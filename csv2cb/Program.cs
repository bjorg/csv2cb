/*
 * MindTouch Csv2Cb - Import CSV files into Couchbase
 * Copyright (C) 2013 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit wiki.developer.mindtouch.com;
 * please review the licensing section.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Couchbase;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using MindTouch.Csv;
using MindTouch.Dream;
using NDesk.Options;
using Newtonsoft.Json;

namespace MindTouch.Csv2Cb {
    internal class MainClass {

        //--- Constants ---
        private const int MIN_RECORDS = 10000;
        private const int THREAD_COUNT = 8;
        private const int MAX_RETRIES = 12;
        private const int COUCHBASE_TEMPORARY_OUT_OF_MEMORY = 134;
        private const int COUCHBASE_OUT_OF_MEMORY = 130;

        //--- Class Fields ---
        private static CouchbaseClient _client;
        private static volatile int _skippedRecords;
        private static volatile int _threadCount;

        //--- Class Methods ---
        public static void Main(string[] args) {
            Console.WriteLine("MindTouch CSV2CB - Import CSV files into Couchbase");
            string password = null;
            XUri host = null;
            string doctype = null;
            bool help = false;
            var options = new OptionSet {
                { "host=", v => host = XUri.TryParse(v) },
                { "password=", v => password = v },
                { "doctype=", v => doctype = v },
                { "h|?|help", v => help = (v != null) }
            };
            var filenames = options.Parse(args);

            // show help if requested
            if(help) {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            // create client and verify connection
            try {
                _client = CreateClient(host, password);
                var key = "test:" + StringUtil.CreateAlphaNumericKey(16);
                if(!_client.Store(StoreMode.Set, key, "succcess!")) {
                    throw new ApplicationException("unable to connect to couchbase server");
                }
                _client.Remove(key);
            } catch(Exception e) {
                Console.Error.WriteLine("ERROR: {0}", e.Message);
                return;
            }

            // process all files
            foreach(var filename in filenames) {
                if(!File.Exists(filename)) {
                    Console.Error.WriteLine("ERROR: unable to find '{0}'", filename);
                    return;
                }
                try {
                    var table = CsvTable.NewFromPath(filename, Encoding.UTF8, null);
                    var records = new List<KeyValuePair<string, string>>();
                    foreach(var row in table) {
                        var key = StringUtil.CreateAlphaNumericKey(16);
                        if(!string.IsNullOrEmpty(doctype)) {
                            key = doctype + ":" + key;
                        }
                        var value = RowToJson(row, doctype);
                        records.Add(new KeyValuePair<string, string>(key, value));
                    }
                    if(records.Any()) {
                        int skipped;
                        Send(records, out skipped);
                    }
                } catch(Exception e) {
                    Console.Error.WriteLine("ERROR: error loading file '{0}': {1}", filename, e.Message);
                    return;
                }
            }
        }

        private static CouchbaseClient CreateClient(XUri host, string password) {
            if(!host.HostIsIp) {
                var hostEntry = Dns.GetHostEntry(host.Host);
                var ip = hostEntry.AddressList[0].ToString();
                host = host.WithHost(ip);
            }
            var config = new CouchbaseClientConfiguration();
            config.Urls.Add(host.WithoutPathQueryFragment().At("pools"));
            config.Bucket = !ArrayUtil.IsNullOrEmpty(host.Segments) && (host.Segments.Length == 1) ?  host.Segments[0] : null;
            config.BucketPassword = password;
            return new CouchbaseClient(config);
        }

        private static string RowToJson(CsvTable.Row row, string doctype) {
            var buffer = new StringWriter();
            var writer = new JsonTextWriter(buffer);
            writer.WriteStartObject();
            if(doctype != null) {
                writer.WritePropertyName("doctype");
                writer.WriteValue(doctype);
            }
            for(var i = 0; i < row.Table.Headers.Length; ++i) {
                writer.WritePropertyName(row.Table.Headers[i]);
                var value = row[i];
                double number;
                if(double.TryParse(value, out number)) {
                    if(double.IsNaN(number) || double.IsInfinity(number)) {
                        writer.WriteNull();
                    } else {
                        writer.WriteRawValue(value);
                    }
                } else {
                    writer.WriteValue(value);
                }
            }
            writer.WriteEndObject();
            return buffer.ToString();
        }

        public static void Send(IEnumerable<KeyValuePair<string, string>> records, out int skipped) {
            Console.Error.Write("Uploading data...");
            _skippedRecords = 0;
            if(records.Count() < MIN_RECORDS) {
                Send_Helper(records);
            } else {
                var count = records.Count() / THREAD_COUNT;
                for(var i = 0; i < THREAD_COUNT; ++i) {
                    var subRecords = records.Skip(i * count);
                    if(i < THREAD_COUNT - 1) {
                        subRecords = subRecords.Take(count);
                    }
                    Interlocked.Increment(ref _threadCount);
                    new Thread(() => Send_Helper(subRecords)).Start();
                }
                while(_threadCount > 0) {
                    Thread.Sleep(250);
                }
            }
            skipped = _skippedRecords;
            Console.Error.WriteLine("done");
        }

        private static void Send_Helper(IEnumerable<KeyValuePair<string, string>> records) {
            var skipped = 0;
            try {
                var random = new Random();
                var sent = 0;
                foreach(var record in records) {
                    while(true) {
                        var result = _client.ExecuteStore(StoreMode.Add, record.Key, record.Value);
                        if(result.Success) {
                            ++sent;
                            break;
                        }
                        if(result.StatusCode == COUCHBASE_TEMPORARY_OUT_OF_MEMORY) {
                            
                            // couchbase needs to catch-up on writing to disk; take a break and continue
                            Thread.Sleep((int)(100 + random.NextDouble() * 300));
                            continue;
                        }
                        if(result.StatusCode == COUCHBASE_OUT_OF_MEMORY) {
                            
                            // couchbase has run out of disk space; time to give up
                            skipped = records.Count() - sent;
                            return;
                        }
                        
                        // unknown error encountered
                        if(result.StatusCode.HasValue) {
                            Console.Error.Write("{0} ", result.StatusCode);
                        } else {
                            Console.Error.Write("!");
                        }
                        ++skipped;
                        break;
                    }
                }
            } finally {
                Interlocked.Add(ref _skippedRecords, skipped);
                Interlocked.Decrement(ref _threadCount);
            }
        }
    }
}
