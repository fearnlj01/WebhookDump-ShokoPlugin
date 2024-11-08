# What is this?
Flawed, but functional :)

If you use discord and want to be *automatically* notified when Shoko is not able to recognise any media it's found so
you can get it added to AniDB, this could be the plugin for you.
___________
## Features
This [Shoko](https://shokoanime.com/) plugin offers a few different features, some required, some optional. This plugin
was built for my own benefit, so forgive me if some things don't make sense.

### Required
- Automatic AVDumping of unmatched files (first time unmatched only)

### Optional
##### Webhooks
If enabled and provided with a valid webhook URL, when a file cannot be matched by Shoko it will send an embed message to discord.

When a file cannot be automatically matched:
- Provides a link to the `Unrecognised files` utility in the Shoko Web UI.
- Provides the result of the AVDump in a copy/paste friendly format.
- Provides links directly to the `Add Release` page for the top three most likely anime matching the file.
  - If **the most directly matching title** is R18, you can optionally prevent the message from being sent.
  - If **any of the top 10** most likely series are R18, you can optionally prevent these individual titles from being linked.
  - If a series is deemed by Shoko as being currently airing... It will get bumped to be the first title shown.
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
If enabled, each file that's been handled by this plugin can automatically try get Shoko to match the file again. This
can be done in a combination of two ways:
- Scheduled
  - Whenever Shoko tries to match a file being managed by the plugin, a future match attempt will be scheduled.
  - For each attempt, it will wait `i * 5` minutes before the next attempt is scheduled.
  - Unless changed, the plugin will only attempt this for up to five match attempts.
- Watching for reactions to the webhook message
  - For any webhook sent since Shoko was last restarted, it will check the message state every 15 minutes.
  - If the message has any reactions when checked, it will queue a rescan for the file.
  - This will continue until there is no reactions, or the file is matched - be warned that this may result in Shoko getting a (temporary) AniDB ban.
___
## What do the messages look like?

File unmatched

![Webhook Example Image - File Unmatched](https://i.imgur.com/DQRmnoL.png)

File unmatched (Matching CRC not found)

![Webhook Example Image - File Unmatched - No matching CRC](https://i.imgur.com/TNH0kQB.png)

File matched

![Webhook Example Image - File Matched](https://i.imgur.com/6eUehg6.png)

# Installation instructions

1. Download `WebhookDump.dll` from the latest release (or follow the build instructions to create this)
2. Find the config directory for [Shoko Server](https://github.com/ShokoAnime/ShokoServer/).
   - Windows: `C:\ProgramData\ShokoServer\`
   - Docker Compose ([recommended on linux](https://docs.shokoanime.com/getting-started/installing-shoko-server)): `./shoko-config/Shoko.CLI/`
   - Linux: `$HOME/.shoko/Shoko.CLI/`
3. Copy `WebhookDump.dll` into the `plugins` directory. You may need to create the directory yourself.
4. Relaunch Shoko Server (this creates the settings file mentioned below).
5. Edit (and save) the configuration file found in the config directory, `WebhookDump.json`.
6. Relaunch Shoko Server and enjoy!

## Plugin setup

Everything in the plugin is configured using a JSON file, `WebhookDump.json`, created in the Shoko Server config
directory. Unfortunately there's not currently a way of changing the settings in a UI, but who knows what PR's people
may submit?

An example fully configured settings file is shown below
```json
{
  "Shoko": {
    "ApiKey": "f6207f72-7323-48fd-bbe7-b246a299131e",
    "ServerPort": 8111,
    "PublicUrl": "https://shoko.mywebsite.com",
    "PublicPort": null,
    "AutomaticMatch": {
      "Enabled": true,
      "MaxAttempts": 5,
      "WatchReactions": false
    }
  },
  "Webhook": {
    "Enabled": false,
    "Url": "https://discord.com/api/webhooks/1304522890594488445/XTgEytkQGhHE1w6ANTDavyuJ01ol_2DCt8HAVq0Z6t1wcKscs4rjeJN5qS2kqNf6LSK9",
    "Username": "Shoko",
    "AvatarUrl": "https://raw.githubusercontent.com/ShokoAnime/ShokoServer/e88bb42b544809334daaf9053ae9582601d90915/.github/images/Shoko.png",
    "Matched": {
      "MessageText": null,
      "EmbedText": "An unmatched file automatically dumped by this plugin has now been matched.",
      "EmbedColor": "#57F287"
    },
    "Unmatched": {
      "MessageText": null,
      "EmbedText": "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
      "EmbedColor": "#3B82F6"
    },
    "Restrictions": {
      "ShowRestrictedTitles": false,
      "PostIfTopMatchRestricted": true
    }
  }
}
```

## Getting the (Shoko) API key

There used to be more documented ways in the past here,
but you know what you're doing if you don't want to use this option.

### From the Web UI
1. Log into the Web UI
2. Open the Settings.
3. Choose the menu option called "API Keys".
4. Input a name, click "Generate," and copy the provided key.

# Build instructions

1. Clone this repository & ensure that at least v8.0 of the .NET Core SDK is installed
2. Leave a like, subscribe and smash that bell button
3. Run the below commands

```sh
dotnet restore
dotnet build -c Release
```

4. `WebhookDump.dll` should've been built and be ready to copy from the `bin/Release/net8.0/` folder.
