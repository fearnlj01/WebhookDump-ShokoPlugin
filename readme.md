# Features
This [Shoko](https://shokoanime.com/) plugin offers a few different features, some required, some optional. This plugin
was built for my own benefit, so please do forgive me if some things don't make sense.

## Required
- Automatic AVDumping of unmatched files (which will happen the first time is found by Shoko and not matched to a
show/episode)

## Optional
### Webhooks
If enabled and provided with a valid webhook URL, when a file cannot be matched by Shoko it will send an embed message
to discord.

When a file cannot be automatically matched:
- Provides a link to the `Unrecognised files` utility in the Shoko Web UI.
- Provides the result of the AVDump in a copy/paste friendly format.
- Provides links directly to the `Add Release` page for the top three most likely anime matching the file.
  - If **the most directly matching title** is R18, you can optionally prevent the message from being sent.
  - If **any of the top 10** most likely series are R18, you can optionally prevent these individual titles from being
linked.
  - If one of the likely matches is deemed by Shoko as being currently airing... It will get bumped to be the first
title shown.
- In the footer of the embed, includes:
  - The Shoko ID for the file
  - The computed CRC for the file.
  - If the CRC is not found in the original title of the file... A notification as such will also be shown.
- If the CRC is not found in the original title, the embed colour will also change (not currently configurable).

After a file (previously unmatched) is matched, the previous message will be edited to have:
- A copy of the series poster as a thumbnail
- A link to the series on AniDB
- A link to the episode on AniDB
- A relative timestamp, letting you know in a human-readable format when the file was recognised by Shoko

##### Automatic match attempts
If enabled, each file that's been handled by this plugin can automatically request that Shoko attempts to match the file
again. This can be done in a combination of two ways:
- Scheduled
  - Whenever Shoko tries to match a file being managed by the plugin, a future match attempt will be scheduled.
  - With each failed attempt at finding a match, the plugin will wait an exponentially increasing amount of time before
it requests that Shoko searches for matches. _(This mitigates the chances of you getting banned from AniDB, which by all
means may still happen)_
  - Unless changed, the plugin will only attempt this for up to eight match attempts (over a window of ~39 Hours).
- Watching for reactions to the webhook message
  - For every webhook sent by the plugin (for files that haven't later been matched), the plugin will check the sent
message state every 15 minutes.
  - If any message has a reaction on it, a rescan for the file will be queued.

___
## What do the messages look like?

File unmatched

![Webhook Example Image - File Unmatched](https://i.imgur.com/DQRmnoL.png)

File unmatched (Matching CRC not found)

![Webhook Example Image - File Unmatched - No matching CRC](https://i.imgur.com/TNH0kQB.png)

File matched

![Webhook Example Image - File Matched](https://i.imgur.com/6eUehg6.png)

# Installation instructions
> [!WARNING]
> If this is visible... Then please note that Shoko Server v6.0.0 is required for this plugin to work.
> At the time of writing, March 2026, Shoko Server v6.0.0 is still in development and this plugin may break as part of
> the changes made.

1. Download `WebhookDump.dll` from the latest action/release (or follow the build instructions to create this)
   - The latest GitHub actions can be found [here](https://github.com/fearnlj01/WebhookDump-ShokoPlugin/actions/workflows/dev-build.yml?query=branch%3Adev+is%3Asuccess)
. The latest file should be available to download at the bottom of the page in an `Artifacts` section.
   - If there is no file available to download, please refer to the latest release, or alternatively build the plugin
yourself.
2. Find the config directory for [Shoko Server](https://github.com/ShokoAnime/ShokoServer/).
   - Windows: `C:\ProgramData\ShokoServer\`
   - Docker Compose ([recommended on linux](https://docs.shokoanime.com/getting-started/installing-shoko-server)): `./shoko-config/Shoko.CLI/`
   - Linux: `$HOME/.shoko/Shoko.CLI/`
3. Copy `WebhookDump.dll` into the `plugins` directory. You may need to create the directory yourself.
4. Relaunch Shoko Server
5. Go through the [plugin setup](#plugin-setup) process
6. Relaunch Shoko Server and enjoy!

## Plugin setup
> [!WARNING]
> The plugin now uses the Shoko-provided configuration options that are made available. Any changes made on Shoko Server
> may result in the instructions below being out of date.
>
> Previous versions (less than v2.0.0) of the plugin's configuration will not be migrated automatically and will require manual
> reconfiguration.

Currently, the easiest way to discover the configuration and update it is using swagger, which on a default Shoko Server
instance running on your computer is available at `https://localhost:8111/swagger/index.html`.

You'll want to have an API key ready for use by swagger, which can be generated in line with Shoko's [official
documentation](https://docs.shokoanime.com/shoko-server/settings#api-keys).
You can then use the `GET` `/Configuration/{id}` endpoint with the following IDs to see the plugin's available
configuration options:
- `5e062b5b-41b5-5708-9d83-8f0837aadcf3`
- `a9a619d1-98cc-5822-bb28-1fa11ee5b511`

After you've got your new configuration ready, you can update the plugin's configuration by using the `PUT`
`/Configuration` endpoint. Restarting the server after updating the configuration is recommended.

# Build instructions

1. Clone this repository and ensure that at least v10.0 of the .NET Core SDK is installed
2. Run the below commands

```sh
dotnet restore
dotnet build -c Release
```

3. `WebhookDump.dll` should've been built and be ready to copy from the `bin/Release/net10.0/` folder.
