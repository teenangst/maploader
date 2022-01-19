# Map Loader

Load Team Fortress 2 maps with ease. The `maps` command in-game works well if you only have a hundred maps, but once you get to many hundreds or thousands the `maps` command is so slow and you have to mentally keep track of where you are. This application will provide an easy way to find and filter maps from your `/tf/maps` and `/tf/download/maps` folders.

With Map Loader type in what you want to play, so for instance koth alpha and beta maps search `koth a b` and you will be presented with alpha and beta koth maps with one selected. Use arrow keys to navigate up and down and press enter to play (make sure to have TF2 open, this application with hijack the game and make it load). You can also press the `f` key to view the forum post if one could be found (all maps with an author listed and some maps without), you can also press `a` to view the author's forum account. By default 25 maps will be shown per page, just move your cursor down to the bottom and the next page is shown, just to avoid a massive list all at once.
You can search for most gamemodes from `cp` to `af`, you can also search for versions `a`/`alpha`, `b`/`beta`, `rc`, `f`/`final`. You will be presented with a readout so you can be sure your command was understood `koth a b` => `King of the Hill where Alpha, Beta`. A thing to note is that it is not required to give a gamemode or a version, you can search for all koth maps with `koth` or all rc and final maps with `rc f`.
Due to how naming isn't strictly enforced some maps may be missed, maps with version `alpha1` for instance will be found as alpha but it is possible someone has named their map something that kinda makes sense but I didn't consider. This whole system works by generating a regex pattern with gamemode, a bridge, and a version.

Alternatively if you wish to look up all maps that contain the word snow you can search `!snow`, using `!` will allow you to search maps using regex, so you can also search for all maps that have at least 2 words (where the words are at least 2 characters, e.g. `koth_first_second_a1`) with `!([^_]{2,}_){3,}[^_]+`.

Currently only 1 special command exists, but if you would like to sort maps by date you can use `@date` followed by `desc` to sort newest first or `asc` to sort oldest first.

If you need any help feel free to ping Skylark in the TF2Maps Discord
