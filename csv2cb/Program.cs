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
using System.IO;
using System.Text;
using MindTouch.Csv;
using NDesk.Options;
using Newtonsoft.Json;

namespace MindTouch.Csv2Cb {
    internal class MainClass {

        //--- Class Methods ---
        public static void Main(string[] args) {
            Console.WriteLine("MindTouch CSV2CB - Import CSV files into Couchbase");
            string password = null;
            string bucket = null;
            string doctype = null;
            bool help = false;
            var options = new OptionSet {
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
            foreach(var filename in filenames) {
                if(!File.Exists(filename)) {
                    Console.Error.WriteLine("ERROR: unable to find '{0}'", filename);
                    return;
                }
                try {
                    var table = CsvTable.NewFromPath(filename, Encoding.UTF8, null);
                    foreach(var row in table) {
                        var json = RowToJson(row, doctype);

                        // TODO: emit to couchbase
                        Console.WriteLine(json);
                    }
                } catch(Exception e) {
                    Console.Error.WriteLine("ERROR: error loading file '{0}': {1}", filename, e.Message);
                    return;
                }
            }
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
    }
}
