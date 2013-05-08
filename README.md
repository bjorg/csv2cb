CSV2CB - Import CSV files into Couchbase
========================================

CSV2CB is a command line tool to convert a CSV file into JSON document and upload them to a 
[Couchbase](http://www.couchbase.com/) bucket.  Multiple CSV files can be uploaded to the same Couchbase
bucket in a single invocation.

Usage
-----

    csv2cb --host=<hostname>
           [ --password=<pwd> ]
           [ --doctype=<doctype> ]
           [ --select=<columns> ]
           <file> ...


Command Line Options
--------------------

<dl>
<dt>
        --host=VALUE
</dt>
<dd>
        Couchbase hostname with bucket name.<br/>
        <em>Example:</em> <code>--host=http://example.com:8091/bucket</code>
</dd>
<dt>
        --password=VALUE
</dt>
<dd>
        (optional) Password for Couchbase bucket.
</dd>
<dt>
        --doctype=VALUE
</dt>
<dd>
        (optional) Document type identifier (doctype) to add to each converted JSON document.<br/>
        <em>Example:</em> <code>--doctype=customer</code>
</dd>
<dt>
        --select=VALUES
</dt>
<dd>
        (optional) List of CSV columns to import. By default, all columns are imported.<br/>
        <em>Example:</em> <code>--select=id,name</code>
</dd>
<dt>
        -h, -?, --help
</dt>
<dd>
        Shows the tool's help text.
</dd>
</dl>

Sample Invocation
-----------------

    csv2cb --doctype=customer --select=id,name --host=http://example.com:8091/customers customers.csv

    MindTouch CSV2CB - Import CSV files into Couchbase

    Loading file 'customers.csv'...done (10,000 rows)
    Converting rows...done (10,000 documents)
    Importing documents...done (10,000 documents)

    2.208 seconds elapsed (loading: 0.197s, converting: 0.253s, importing: 1.758s)
    10,000 records loaded (50,816.132 records/second)
    10,000 records converted (39,512.073 records/second)
    10,000 records imported (5,689.450 records/second)

Requirements
------------
CSV2CB requires either .Net 3.5 or Mono 2.10 to run.

License
-------
Copyright 2013 MindTouch Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

[http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0])

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
