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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MindTouch.Csv {
    public class CsvTable : IEnumerable<CsvTable.Row> {

        //--- Types ---
        public class Row {

            //--- Fields ---
            private readonly CsvTable _table;
            private readonly string[] _values;

            //--- Constructors ---
            internal Row(CsvTable table, string[] values) {
                if(table == null) {
                    throw new ArgumentNullException("table");
                }
                if(values == null) {
                    throw new ArgumentNullException("values");
                }
                _table = table;
                _values = values;
            }

            //--- Properties ---
            public string PrimaryKey {
                get {
                    if(_table._primaryKeyColumnIndex < 0) {
                        return null;
                    }
                    return _values[_table._primaryKeyColumnIndex];
                }
            }

            public IEnumerable<string> Values { get { return _values; } }
            public CsvTable Table { get { return _table; } }


            //--- Operators ---
            public string this[string column] { get { return _values[_table._columns[column]]; } }
            public string this[int index] { get { return _values[index]; } }
        }

        //--- Class Methods ---
        public static CsvTable NewFromPath(string path, Encoding encoding, string primaryKeyColumn) {
            using(var stream = new StreamReader(File.Open(path, FileMode.Open), encoding)) {
                return new CsvTable(stream, primaryKeyColumn);
            }
        }
        
        public static CsvTable NewFromStream(StreamReader stream, string primaryKeyColumn) {
            return new CsvTable(stream, primaryKeyColumn);
        }
        
        public static CsvTable NewWithHeaders(IEnumerable<string> headers, string primaryKeyColumn) {
            return new CsvTable(headers, primaryKeyColumn);
        }
        
        //--- Fields ---
        private readonly string[] _headers;
        private readonly Dictionary<string, int> _columns;
        private readonly List<Row> _rows;
        private readonly Dictionary<string, Row> _index;
        private readonly int _primaryKeyColumnIndex;

        //--- Constructors ---
        private CsvTable(StreamReader stream, string primaryKeyColumn) {
            var csv = new CsvStream(stream);
            _columns = new Dictionary<string, int>();

            // read column headers
            var headerRow = csv.GetNextRow();
            if(headerRow == null) {
                throw new Exception("CSV file is empty");
            }
            _headers = new string[headerRow.Length];
            for(var i = 0; i < headerRow.Length; ++i) {
                var header = headerRow[i];
                _columns[header] = i;
                _headers[i] = header;
            }

            // create index if a primary-key column is defined
            _primaryKeyColumnIndex = -1;
            if(primaryKeyColumn != null) {
                if(!_columns.TryGetValue(primaryKeyColumn, out _primaryKeyColumnIndex)) {
                    throw new Exception(string.Format("primary key column '{0}' not found", primaryKeyColumn));
                }
                _index = new Dictionary<string, Row>();
            }

            // read records
            _rows = new List<Row>();
            for(var recordRow = csv.GetNextRow(); recordRow != null; recordRow = csv.GetNextRow()) {
                var row = new Row(this, recordRow);
                _rows.Add(row);
                if(_primaryKeyColumnIndex >= 0) {
                    _index[recordRow[_primaryKeyColumnIndex]] = row;
                }
            }
        }

        private CsvTable(IEnumerable<string> headers, string primaryKeyColumn) {
            if(headers == null || !headers.Any()) {
                throw new Exception("No headers found");
            }
            _columns = new Dictionary<string, int>();
            _headers = new string[headers.Count()];
            for (var i = 0; i < headers.Count(); ++i) {
                var header = headers.ElementAt(i);
                _columns[header] = i;
                _headers[i] = header;
            }

            // create index if a primary-key column is defined
            _primaryKeyColumnIndex = -1;
            if(primaryKeyColumn != null) {
                if(!_columns.TryGetValue(primaryKeyColumn, out _primaryKeyColumnIndex)) {
                    throw new Exception(string.Format("primary key column '{0}' not found", primaryKeyColumn));
                }
                _index = new Dictionary<string, Row>();
            }
            _rows = new List<Row>();
        }

        //--- Properties ---
        public int RowCount { get { return _rows.Count; } }

        //--- Operators ---
        public string[] Headers { get { return _headers; } }

        public Row this[int rowIndex] { get { return _rows[rowIndex]; } }

        public Row this[string primaryKey] {
            get {
                if(_index == null) {
                    throw new Exception("no primary key column defined");
                }
                Row row;
                _index.TryGetValue(primaryKey, out row);
                return row;
            }
        }

        //--- Methods ---
        public IEnumerator<Row> GetEnumerator() {
            return _rows.GetEnumerator();
        }

        public void AddRange(IEnumerable<IEnumerable<string>> vals) {
            _rows.AddRange(vals.Select(setOfValues => new Row(this, setOfValues.ToArray())));
        }

        public void Save(string path) {
            using(var writer = new StreamWriter(new FileStream(path, FileMode.Create))) {
                writer.WriteLine(Join(_columns.Keys.ToArray()));
                foreach(var row in _rows) {
                    writer.WriteLine(Join(row.Values));
                }
            }
        }

        private static string Join(IEnumerable<string> values) {
            return string.Join(",", values);
        }

        //--- IEnumerable Members ---
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}