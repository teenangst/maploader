open System
open System.IO
open System.Net
open System.Threading
open Newtonsoft.Json
open BlackFox.ColoredPrintf

let version = "a2"

let exitEvent = new System.Threading.ManualResetEvent(false)
let proc = new System.Diagnostics.Process()

let display = [(1, "gray"); (2, "white"); (3, "magenta"); (4, "red"); (18, "darkgray"); (19, "green"); (29, "darkyellow"); (34, "white;darkmagenta"); (35, "white"); (36, "cyan"); (37, "red"); (38, "yellow")] |> Map.ofList

type config = {directories:string array; tf2:string; pageSize:int; showAuthor:bool; launchOptions:string}

let get_tf2_folder () =
  let def = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2"
  if Directory.Exists def then def
  else
    let rec getting () =
      printf "Enter the path for your 'Team Fortress 2' folder >"
      let dir = Console.ReadLine ()
      if
        Directory.Exists dir
        && Path.Combine(dir, @"tf") |> Directory.Exists
        && Path.Combine(dir, @"hl2.exe") |> File.Exists
      then
        dir
      else
        getting ()
    getting ()

let Config =
  if File.Exists "config.json" then
    File.ReadAllText "config.json" |> JsonConvert.DeserializeObject<config>
  else
    let tf2 = get_tf2_folder ()
    let cfg:config = {
      directories=[|
        Path.Combine(tf2, @"tf\maps");
        Path.Combine(tf2, @"tf\download\maps");
      |];
      tf2=Path.Combine(tf2, @"hl2.exe");
      pageSize=25;
      showAuthor=true;
      launchOptions="-novid -windowed -noborder -condebug"
    }
    File.WriteAllText("config.json", JsonConvert.SerializeObject cfg)
    cfg

let unixtime = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds |> int64) - 604800L

type mapinfo = {userid: int; forumid:int}
type userinfo = {username:string; userid: int; display:int; updated:int64}
let mutable cache = 
  if File.Exists "cache.json" then
    File.ReadAllText "cache.json" |> JsonConvert.DeserializeObject<Map<string,mapinfo>>
  else
    Map.empty
let mutable users:Map<int,userinfo> = 
  if File.Exists "users.json" then
    File.ReadAllText "users.json" |> JsonConvert.DeserializeObject<Map<int,userinfo>>
  else
    Map.empty<int,userinfo>

type apiforumuser = {id:int; username:string; custom_title:string; display:int; avatar:string; updated:int64}
type apimapinfo = {map:string; url:string; notes:string; forum_id:int; forum_version:int; forum_icon:string; forum_username:string; forum_user_id:int; forum_tagline:string; forum_user:apiforumuser; discord_user_handle:string; added:int64}

let maps,maps_files =
  let files = 
    Config.directories
    |> Array.toList
    |> List.map(fun dir ->
      Directory.GetFiles(dir)
      |> Array.filter(fun x -> x.Contains(".bsp") && not (x.Contains(".bz2")))
      |> Array.toList
    )
    |> List.reduce List.append
  files
  |> List.map(fun x -> Path.GetFileNameWithoutExtension(x))
  |> List.sort
  |> List.distinct
  , files |> List.map (fun f -> new FileInfo(f))

let getUsername map =
  if Config.showAuthor = false || (cache.ContainsKey map |> not) then ""
  else
    if cache.[map].userid = 0 || (users.ContainsKey (cache.[map].userid) |> not) then ""
    else if display.ContainsKey (users.[cache.[map].userid].display) |> not then sprintf "| %s" users.[cache.[map].userid].username
    else
      sprintf "¦ $%s[%s]" display.[users.[cache.[map].userid].display] users.[cache.[map].userid].username

let rec start () =
  let rec displayMaps top page (maps:string List) =
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
        displayMaps top (page+1) maps
      else
        //colorprintf "$green[> %s]" (maps.[Console.CursorTop - top])
        colorprintf (ColorPrintFormat(sprintf "$green[> %s] %s" (maps.[Console.CursorTop - top]) (getUsername maps.[Console.CursorTop - top])))
      Console.CursorLeft <- 0

    let _m =
      maps 
      |> List.skip((page * Config.pageSize)) 
    _m |> List.take(if _m.Length > Config.pageSize then Config.pageSize else _m.Length) 
    |> List.iteri(fun i x ->
      if i = 0 then
        //colorprintfn "$green[> %s]" x
        colorprintfn (ColorPrintFormat(sprintf "$green[> %s] %s" x (getUsername x)))
      else
        //printfn "  %s" x
        colorprintfn (ColorPrintFormat(sprintf "  %s %s" x (getUsername x)))
    )
    if _m.Length >= Config.pageSize-1 then
      let _m2 = maps |> List.skip(((1+page) * Config.pageSize))
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

      let message = sprintf "%s%s%s" (firstDesc |> List.map(fun x -> sprintf "$green[%s]" x) |> String.concat "$gray[, ]") (if firstDesc.Length > 0 && lastDesc.Length > 0 then "$gray[ where ]" else "") (lastDesc |> List.map(fun x -> sprintf "$green[%s]" x) |> String.concat "$gray[, ]")
      colorprintfn (ColorPrintFormat(message))
      new System.Text.RegularExpressions.Regex(output)
  
  (*let load_map_files () =
    if maps_files.Length = 0 then
      //colorprintfn "$yellow[Loading file info]"
      maps_files <-
        maps
        |> List.map (fun f -> new FileInfo(f))
        //|> Array.toList*)
  //try
  Console.CursorVisible <- true
  printf "Enter query to filter maps by: "
  Console.ForegroundColor <- ConsoleColor.Cyan
  //let filter = new System.Text.RegularExpressions.Regex(Console.ReadLine())
  let command = Console.ReadLine()
  if command.StartsWith "@" then
    if command.Split(' ').Length <> 2 then
      start ()
    else
      let cmd_parts = command.Split(' ')
      colorprintfn "Using special command $yellow[%s]" command
      match command.ToLower() with
      | "@date desc" ->
        maps_files
        |> List.sortByDescending(fun f -> f.CreationTime)
        |> List.map(fun f -> Path.GetFileNameWithoutExtension(f.Name))
        |> displayMaps Console.CursorTop 0
      | "@date asc" -> 
        maps_files
        |> List.sortBy(fun f -> f.CreationTime)
        |> List.map(fun f -> Path.GetFileNameWithoutExtension(f.Name))
        |> displayMaps Console.CursorTop 0
      | _ ->
        cmd_parts.[0]
        |> colorprintfn "$red[Unrecognised special command %s]"
        start ()
  else
    let filter = parseCommand(command)
    
    let filteredMaps = maps |> List.filter(fun x -> filter.IsMatch x)
    if filteredMaps.Length = 0 then
      colorprintfn "$red[%A doesn't match any maps]" filter
      start ()
    else
      colorprintfn "There are %i results" filteredMaps.Length
      displayMaps Console.CursorTop 0 filteredMaps
  //with _ ->
  //  colorprintfn "$red[Regex was invalid.]"
  //  start()

let loading = [|"/"; "-"; "\\"; "|"|]

let mergeMaps = Map.fold (fun acc key value -> Map.add key value acc)

let initialiseCache () =
  let wc = new WebClient()
  let mutable alreadyParsedUsers:Map<int,userinfo> = Map.empty
  let _m = maps |> List.filter(fun x -> cache.ContainsKey x |> not)
  //colorprintfn "$cyan[Updating cache]"
  //colorprintf "$gray[Updating users]"
  //Console.CursorTop <- Console.CursorTop - 1
  //Console.CursorLeft <- 0
  _m |> List.iteri(fun i map ->
    Console.Title <- sprintf "Map Loader %s [initialising cache %i/%i]" version (i+1) (_m.Length)
    //colorprintf "$cyan[Updating cache (%i/%i) %s %s                  ]" (i+1) _m.Length loading.[i%4] map
    //Console.CursorLeft <- 0
    try
      let res = wc.DownloadString(sprintf "https://api.skylarkx.uk/mapinfo?map=%s" map)
      let api = res |> JsonConvert.DeserializeObject<apimapinfo>
      cache <- cache.Add(map, {userid=api.forum_user_id; forumid=api.forum_id})
      //printfn "'%s':%A %b" (api.map) (api.forum_user) (api.map = null)

      if api.map <> null then
        if users.ContainsKey(api.forum_user_id) then users <- users.Remove(api.forum_user_id)
        users <- users.Add(api.forum_user_id, {username=api.forum_username; userid= api.forum_user_id; display=api.forum_user.display; updated=api.forum_user.updated})

      if api.map <> null && alreadyParsedUsers.ContainsKey api.forum_user_id |> not then
        alreadyParsedUsers <- alreadyParsedUsers.Add(api.forum_user_id, {username=api.forum_user.username; userid= api.forum_user.id; display=api.forum_user.display; updated=api.forum_user.updated})

      if i%25 = 0 then
        File.WriteAllText("cache.json", cache |> JsonConvert.SerializeObject)
        File.WriteAllText("users.json", users |> JsonConvert.SerializeObject)
    with
    | e -> 
      cache <- cache.Add(map, {userid=0; forumid=0})
      //colorprintfn "$red[%s] for %s" e.Message map
  )
  //Console.CursorLeft <- 0
  File.WriteAllText("cache.json", cache |> JsonConvert.SerializeObject)
  //colorprintfn "$darkgray[Updated cache                                                        ]"
  //colorprintfn "$cyan[Updating users]"

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
  //Console.CursorTop <- Console.CursorTop - 1
  //colorprintfn "$darkgray[Updated users ]"
  Console.Title <- sprintf "Map Loader %s" version

let () =
  Console.Title <- sprintf "Map Loader %s" version
  colorprintfn "$yellow[Map Loader %s by Skylark#6969.\nPlease see https://skylarkx.uk/maploader for help.\nThere are %i maps available]" version maps.Length
  let latestVersion = (new WebClient()).DownloadString(sprintf "https://raw.githubusercontent.com/teenangst/maploader/main/version.txt?%i" unixtime).Split('\n')
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
    let work:ThreadStart = initialiseCache
    let thread:Thread = new Thread(work)
    thread.Start()
    //initialiseCache ()

  proc.StartInfo.FileName <- Config.tf2

  Console.CancelKeyPress.AddHandler(fun _ e -> 
    exitEvent.Set() |> ignore
    Environment.Exit 0
  )

  start ()
  exitEvent.WaitOne() |> ignore
