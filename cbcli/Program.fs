(*
 * MindTouch CBCLI - Commandline interface for Couchbase
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
 *)

module MindTouch.Couchbase.CLI

open System
open System.Linq
open Couchbase
open Couchbase.Configuration
open Enyim.Caching.Memcached
open MindTouch.Dream


let MAX_DOCUMENTS = 1000

let DeleteRow (client : CouchbaseClient) (row : IViewRow) =
    printf "."
    client.Remove(row.ItemId) |> ignore

let PrintRow (client : CouchbaseClient) (row : IViewRow) =
    printf "Row:"
    for key in row.Info.Keys.OrderBy(fun key -> key) do
        let v = row.Info.[key];
        printf " %s=%s" key (if v <> null then v.ToString() else "null")
    printfn ""

type Command =
    | Help
    | Error of String
    | Action of XUri * (CouchbaseClient -> IViewRow -> Unit)

let ShowUsage (error : String) =
    
    // TODO (steveb): better command line args
    // --host=<hostname>
    // --query=<design/view>
    // --limit=<100>
    
    printfn "MindTouch CBCLI - Commandline interface for Couchbase"
    printfn ""
    if error <> null then
        printfn "ERROR: %s" error
        printfn ""
    printfn "usage: cbcli <hostname> <command> ..."
    printfn ""
    printfn "      <hostname>             Couchbase hostname with bucket, design, and view name."
    printfn "                               Example: http://example.com:8091/bucket/design/view"
    printfn "      <command>              Command to execute on rows."
    printfn "        print                  Print all rows returned by view."
    printfn "        delete                 Delete all rows returned by view."
    printfn "        help                   This help text."

let ParseArgs (args : String[]) =
    if args.Length <> 2 then
        Error "two arguments are required"
    else
        let host = XUri.TryParse(args.[0])
        if host = null then
            Error "hostname is not valid"
        elif host.Segments.Length <> 3 then
            Error "hostname must have three segments: bucket/design_doc/view"
        else
            match args.[1] with
                | "help" -> Help
                | "delete" -> Action(host, DeleteRow)
                | "print" -> Action(host, PrintRow)
                | _ -> Error(sprintf "unknown command '%s'" args.[1])

let ActionOnAllRows (host : XUri) (password : String) (act : CouchbaseClient -> IViewRow -> Unit) =

    // initialize couchbase client
    printf "Initializing client..."
    let config = new CouchbaseClientConfiguration()
    config.Urls.Add(host.WithoutPathQueryFragment().At("pools").ToUri())
    config.Bucket <- host.Segments.[0]
    config.BucketPassword <- password
    let client = new CouchbaseClient(config)
    printfn "done."
    
    // verify that we can connect to the couchbase bucket
    printf "Testing connection..."
    let key = "test:" + StringUtil.CreateAlphaNumericKey(16)
    if not(client.Store(StoreMode.Set, key, "success!")) then
        failwith "unable to connect to couchbase server"
    client.Remove(key) |> ignore
    printfn "done."
    
    // initialize action and view
    let view = client.GetView(host.Segments.[1], host.Segments.[2]) 
    
    // create recursive function to retrieve all records matched by the view
    let rec fetchNextRows (docid : String) = 
        let hasDocId = docid <> null
        printf "Fetching records for %s/%s" (host.Segments.[1]) (host.Segments.[2])
        if hasDocId then
            printf " starting at docid=%s..." (docid.ToString())
        else
            printf "..."
        let rows = (if hasDocId then view.StartKey(docid).Skip(1) else view).Stale(StaleMode.False).Reduce(false).Limit(MAX_DOCUMENTS).ToArray()
        printfn "done (%i records)." (rows.Length)
        for row in rows do
            try
                act client row
            with
                | e -> printfn "Error: %s (id=%s)" (e.ToString()) (row.ItemId.ToString())
        printfn ""
        if rows.Length = MAX_DOCUMENTS then
            fetchNextRows <| rows.Last().ItemId
        else
            ()

    // first call has no starting document ID
    fetchNextRows null

[<EntryPoint>]
let Main args = 

    // TODO (steveb): add support for passwords
    let password = null

    match ParseArgs args with
        | Help -> ShowUsage null
        | Error error -> ShowUsage error
        | Action(host, act) -> ActionOnAllRows host password act
    0
