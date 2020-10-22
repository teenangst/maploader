open System
open System.IO
open System.Net
open Newtonsoft.Json
open BlackFox.ColoredPrintf

let exitEvent = new System.Threading.ManualResetEvent(false)
let proc = new System.Diagnostics.Process()

let display = [(1, "gray"); (2, "white"); (3, "magenta"); (4, "red"); (18, "darkgray"); (19, "green"); (29, "darkyellow"); (34, "white;darkmagenta"); (35, "white"); (36, "cyan"); (37, "yellow")] |> Map.ofList

type config = {directories:string array; tf2:string; pageSize:int; showAuthor:bool; launchOptions:string}
let Config = File.ReadAllText "config.json" |> JsonConvert.DeserializeObject<config>

type mapinfo = {userid: int; forumid:int}
type userinfo = {username:string; userid: int; display:int; updated:int64}
let mutable cache = 
  if File.Exists "cache.json" then
    File.ReadAllText "cache.json" |> JsonConvert.DeserializeObject<Map<string,mapinfo>>
  else
    Map.empty
let mutable users = 
  if File.Exists "users.json" then
    File.ReadAllText "users.json" |> JsonConvert.DeserializeObject<Map<int,userinfo>>
  else
    Map.empty

type apiforumuser = {id:int; username:string; custom_title:string; display:int; avatar:string; updated:int64}
type apimapinfo = {map:string; url:string; notes:string; forum_id:int; forum_version:int; forum_icon:string; forum_username:string; forum_user_id:int; forum_tagline:string; forum_user:apiforumuser; discord_user_handle:string; added:int64}

let maps =
  Config.directories
  |> Array.map(fun dir -> Directory.GetFiles(dir) |> Array.filter(fun x -> x.Contains(".bsp") && not (x.Contains(".bz2"))))
  |> Array.reduce Array.append
  |> Array.map(fun x -> Path.GetFileNameWithoutExtension(x))
  |> Array.sort
  |> Array.distinct

let getUsername map =
  if Config.showAuthor = false || (cache.ContainsKey map |> not) then ""
  else
    if cache.[map].userid = 0 then ""
    else sprintf "¦ $%s[%s]" display.[users.[cache.[map].userid].display] users.[cache.[map].userid].username

let rec start () =
  let rec displayMaps (maps:string array) top page =
    Console.CursorVisible <- false
    Console.ForegroundColor <- ConsoleColor.Gray
    Console.CursorLeft <- 0

    let redraw i =
      if Console.CursorTop = (top + ((1 + page) * Config.pageSize)) then
        colorprintfn "  $darkgray[[Load %i more...\]]" Config.pageSize
      else
        //printf "  %s" (maps.[Console.CursorTop - top])
        colorprintf (ColorPrintFormat(sprintf "  %s %s" (maps.[Console.CursorTop - top]) (getUsername maps.[Console.CursorTop - top])))
      Console.CursorTop <- Console.CursorTop + i
      Console.CursorLeft <- 0
      if Console.CursorTop = (top + ((1 + page) * Config.pageSize)) then
        colorprintf "$green[> %s]                         " (maps.[Console.CursorTop - top])
        displayMaps maps top (page+1)
      else
        //colorprintf "$green[> %s]" (maps.[Console.CursorTop - top])
        colorprintf (ColorPrintFormat(sprintf "$green[> %s] %s" (maps.[Console.CursorTop - top]) (getUsername maps.[Console.CursorTop - top])))
      Console.CursorLeft <- 0

    let _m =
      maps 
      |> Array.skip((page * Config.pageSize)) 
    _m |> Array.take(if _m.Length > Config.pageSize then Config.pageSize else _m.Length) 
    |> Array.iteri(fun i x ->
      if i = 0 then
        //colorprintfn "$green[> %s]" x
        colorprintfn (ColorPrintFormat(sprintf "$green[> %s] %s" x (getUsername x)))
      else
        //printfn "  %s" x
        colorprintfn (ColorPrintFormat(sprintf "  %s %s" x (getUsername x)))
    )
    if _m.Length >= Config.pageSize-1 then
      let _m2 = maps |> Array.skip(((1+page) * Config.pageSize))
      colorprintfn "  $darkgray[[Load %i more...\]]" (if _m2.Length >= Config.pageSize then Config.pageSize else _m2.Length)
    Console.CursorTop <- top + (page * Config.pageSize)
    let mutable active = true
    while true do
      let key = Console.ReadKey true
      match key.Key with
      | ConsoleKey.UpArrow when Console.CursorTop > top ->
        redraw -1
      | ConsoleKey.DownArrow when Console.CursorTop < (top + ((1 + page) * Config.pageSize) - (if _m.Length < Config.pageSize then Config.pageSize - _m.Length + 1 else 0)) ->
        redraw 1
      | ConsoleKey.Enter ->
        Console.Title <- sprintf "Playing %s" (maps.[Console.CursorTop - top])
        colorprintf (ColorPrintFormat(sprintf "$magenta[> %s] %s" (maps.[Console.CursorTop - top]) (getUsername maps.[Console.CursorTop - top])))
        Console.CursorLeft <- 0
        proc.StartInfo.Arguments <- (sprintf "-hijack +map %s %s" (maps.[Console.CursorTop - top])) Config.launchOptions
        proc.Start() |> ignore
      | ConsoleKey.F when cache.ContainsKey (maps.[Console.CursorTop - top]) && cache.[maps.[Console.CursorTop - top]].forumid <> 0 ->
        System.Diagnostics.Process.Start(sprintf @"https://tf2maps.net/downloads/%i/" cache.[maps.[Console.CursorTop - top]].forumid) |> ignore
      | ConsoleKey.A when cache.ContainsKey (maps.[Console.CursorTop - top]) && cache.[maps.[Console.CursorTop - top]].forumid <> 0 ->
        System.Diagnostics.Process.Start(sprintf @"https://tf2maps.net/downloads/authors/%i/" cache.[maps.[Console.CursorTop - top]].userid) |> ignore
      | ConsoleKey.Escape ->
        Console.CursorTop <- (top + ((1 + page) * Config.pageSize) - (if _m.Length < Config.pageSize then Config.pageSize - _m.Length + 1 else 0)) + 1
        colorprintfn "$yellow[Exiting current search]"
        active <- false
        start ()
      | _ -> ()
  let rec parseCommand (cmd:string) =
    if cmd.StartsWith "!" then
      new System.Text.RegularExpressions.Regex(cmd.Substring(1))
    else
      let mutable first = []
      let mutable firstDesc = []
      let mutable last = []
      let mutable lastDesc = []

      cmd.Split(' ')
      |> Array.iter(fun x ->
        match x with
        | "cp" ->
          first <- ["cp"] |> List.append first
          firstDesc <- ["Control Point"] |> List.append firstDesc
        | "koth" ->
          first <- ["koth"] |> List.append first
          firstDesc <- ["King of the Hill"] |> List.append firstDesc
        | "arena" ->
          first <- ["arena"] |> List.append first
          firstDesc <- ["Arena"] |> List.append firstDesc
        | "ctf" ->
          first <- ["ctf"] |> List.append first
          firstDesc <- ["Capture the Flag"] |> List.append firstDesc
        | "adctf" | "actf" ->
          first <- ["ad?ctf"] |> List.append first
          firstDesc <- ["A/D Capture the Flag"] |> List.append firstDesc
        | "aprilfools" | "af" ->
          first <- ["af"] |> List.append first
          firstDesc <- ["April Fools"] |> List.append firstDesc
        | "mvm" ->
          first <- ["mvm"] |> List.append first
          firstDesc <- ["Mann vs Machine"] |> List.append firstDesc
        | "pass" ->
          first <- ["pass"] |> List.append first
          firstDesc <- ["Passtime"] |> List.append firstDesc
        | "pl" ->
          first <- ["pl"] |> List.append first
          firstDesc <- ["Payload"] |> List.append firstDesc
        | "plr" ->
          first <- ["plr"] |> List.append first
          firstDesc <- ["Payload Race"] |> List.append firstDesc
        | "pd" ->
          first <- ["pd"] |> List.append first
          firstDesc <- ["Player Destruction"] |> List.append firstDesc
        | "rd" ->
          first <- ["rd"] |> List.append first
          firstDesc <- ["Robot Destruction"] |> List.append firstDesc
        | "sd" ->
          first <- ["sd"] |> List.append first
          firstDesc <- ["Special Delivery"] |> List.append firstDesc
        | "tc" ->
          first <- ["tc"] |> List.append first
          firstDesc <- ["Territorial Control"] |> List.append firstDesc
        | "tr" ->
          first <- ["tr"] |> List.append first
          firstDesc <- ["Training"] |> List.append firstDesc
        | _ when x.StartsWith "a" -> 
          last <- ["a"] |> List.append last
          lastDesc <- ["Alpha"] |> List.append lastDesc
        | _ when x.StartsWith "b" -> 
          last <- ["b"] |> List.append last
          lastDesc <- ["Beta"] |> List.append lastDesc
        | "rc" -> 
          last <- ["rc"] |> List.append last
          lastDesc <- ["Release Candidate"] |> List.append lastDesc
        | "final" | _ when x.StartsWith "f" -> 
          last <- ["final"] |> List.append last
          lastDesc <- ["Final"] |> List.append lastDesc
        | _ -> ignore 0
      )
      let mutable output = ""

      if first.Length = 0 then
        output <- "^[^_]+_.*?"
      else
        output <- sprintf "^(%s)_.*?" (first |> String.concat "|")

      if last.Length > 0 then
        output <- output + sprintf "_(%s)[^_]*" (last |> String.concat "|")

      let message = sprintf "%s%s%s $gray[maps]" (firstDesc |> List.map(fun x -> sprintf "$green[%s]" x) |> String.concat "$gray[, ]") (if firstDesc.Length > 0 && lastDesc.Length > 0 then "$gray[ where ]" else "") (lastDesc |> List.map(fun x -> sprintf "$green[%s]" x) |> String.concat "$gray[, ]")
      colorprintfn (ColorPrintFormat(message))
      new System.Text.RegularExpressions.Regex(output)
  try
    Console.CursorVisible <- true
    printf "Enter query to filter maps by: "
    Console.ForegroundColor <- ConsoleColor.Cyan
    //let filter = new System.Text.RegularExpressions.Regex(Console.ReadLine())
    let filter = parseCommand(Console.ReadLine())
    let filteredMaps = maps |> Array.filter(fun x -> filter.IsMatch x)
    if filteredMaps.Length = 0 then
      colorprintfn "$red[%A doesn't match any maps]" filter
      start ()
    else
      colorprintfn "There are %i results" filteredMaps.Length
      displayMaps filteredMaps Console.CursorTop 0
  with _ ->
    colorprintfn "$red[Regex was invalid.]"
    start()

let loading = [|"/"; "-"; "\\"; "|"|]

let unixtime = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds |> int64) - 604800L
let mergeMaps = Map.fold (fun acc key value -> Map.add key value acc)

let initialiseCache () =
  let wc = new WebClient()
  let mutable alreadyParsedUsers:Map<int,userinfo> = Map.empty
  let _m = maps |> Array.filter(fun x -> cache.ContainsKey x |> not)
  colorprintfn "$cyan[Updating cache]"
  colorprintf "$gray[Updating users]"
  Console.CursorTop <- Console.CursorTop - 1
  Console.CursorLeft <- 0
  _m |> Array.iteri(fun i map ->
    colorprintf "$cyan[Updating cache (%i/%i) %s %s                  ]" (i+1) _m.Length loading.[i%4] map
    Console.CursorLeft <- 0
    try
      let res = wc.DownloadString(sprintf "https://api.skylarkx.uk/mapinfo?map=%s" map)
      let api = res |> JsonConvert.DeserializeObject<apimapinfo>
      cache <- cache.Add(map, {userid=api.forum_user_id; forumid=api.forum_id})
      //users <- users.Add(map, {username=api.forum_user.username; userid= api.forum_user.id; display=api.forum_user.display; updated=api.forum_user.updated})
      if api.map <> null && alreadyParsedUsers.ContainsKey api.forum_user_id |> not then
        alreadyParsedUsers <- alreadyParsedUsers.Add(api.forum_user_id, {username=api.forum_user.username; userid= api.forum_user.id; display=api.forum_user.display; updated=api.forum_user.updated})
  
      if i%25 = 0 then
        File.WriteAllText("cache.json", cache |> JsonConvert.SerializeObject)
    with
    | e -> 
      cache <- cache.Add(map, {userid=0; forumid=0})
      colorprintfn "$red[%s] for %s" e.Message map
  )
  Console.CursorLeft <- 0
  File.WriteAllText("cache.json", cache |> JsonConvert.SerializeObject)
  colorprintfn "$darkgray[Updated cache                                                        ]"
  colorprintfn "$cyan[Updating users]"

  let needed = cache |> Map.toList |> List.map(fun (_,v) -> v.userid) |> List.distinct |> List.filter(fun x -> users.ContainsKey x && users.[x].updated < unixtime)
  
  //Get user info for these users, then combine with alreadyParsedUsers and overwrite users
  //let needed = (users |> Map.filter(fun k v -> alreadyParsedUsers.ContainsKey k |> not && v.updated < unixtime)) |> Map.toList |> List.map(fun (k,_) -> string k) |> List.append ids
  let _m2 =
    wc.DownloadString(sprintf "https://api.skylarkx.uk/userinfo?user=%s" (needed |> List.map(fun x -> string x) |> String.concat ","))
    |> JsonConvert.DeserializeObject<Map<int, apiforumuser>>
    |> Map.map(fun k v ->
      {username=v.username; userid= v.id; display=v.display; updated=v.updated}
    )
  users <- users |> mergeMaps alreadyParsedUsers |> mergeMaps _m2
  File.WriteAllText("users.json", users |> JsonConvert.SerializeObject)
  Console.CursorTop <- Console.CursorTop - 1
  colorprintfn "$darkgray[Updated users ]"

let version = "a1"

let () =
  Console.Title <- sprintf "Map Loader %s" version
  colorprintfn "$yellow[Map Loader %s by Skylark#6969.\nPlease see https://skylarkx.uk/maploader for help.\nThere are %i maps available]" version maps.Length
  let latestVersion = (new WebClient()).DownloadString("https://raw.githubusercontent.com/teenangst/maploader/main/version.txt").Split('\n')
  if latestVersion.[0] <> version then
    colorprintfn "$white;darkred[New version %s available! Download at https://skylarkx.uk/maploaderrelease]" latestVersion.[0]
    latestVersion |> Array.take (Array.findIndex(fun line -> line = version) latestVersion)
    |> Array.iteri(fun i line ->
      if i = 0 || latestVersion.[i-1] = "" then
        colorprintfn "$black;cyan[  %s          ]" line
      else if line <> "" then
        colorprintfn "$cyan[%s]" line
    )

  if Config.showAuthor then
    initialiseCache ()

  proc.StartInfo.FileName <- Config.tf2

  Console.CancelKeyPress.AddHandler(fun _ e -> 
    exitEvent.Set() |> ignore
    Environment.Exit 0
  )

  start ()
  exitEvent.WaitOne() |> ignore
