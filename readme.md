# What is this?

~~Badly made, hacked together and published to GitHub far too soon...~~  
Flawed, but at long last, hopefully highly functional :) just don't look at the commit history.

## Features

This [Shoko](https://shokoanime.com/) plugin offers a few different features, primarily targeted at users who want to be
able to add files to AniDB as soon as possible.

- Automatic AVDumping of unmatched files (first time unmatched only)
- Sending webhooks to discord when the file is not matched
  - When unmatched,
    - The dumped ED2K is provided, ready to be added to AniDB
    - The three most likely AniDB series for the file (based on filename) have links provided for the "Add Release" page.
    - The title URL for the embed will take you to the `Unrecognised files` utility in Shoko's beta Web UI.
  - When later matched (overwriting the original message),
    - Series poster attached as a thumbnail to the embed
    - Link to the Anime on AniDB
    - Link to the Episode on AniDB
- Optionally, automatic re-scanning. There are two ways of doing this.
  - Queued on future unmatched events
    - For each time a file has attempted to be matched, the wait till the next attempt will increase by five minutes.
    - By default, rescans will be queued five times. (5 minutes after dumping, then 10, 15, 20 & 25 minutes after the
      last rescan)
  - Watched webhook messages
    - For messages that the plugin is tracking (any sent by the plugin since the server was launched each session), they
      will be checked every fifteen minutes. If a message has any reactions when polled, it will queue a rescan for the
      relevant file.

### What do the webhooks look like?

_File unmatched_  
![Webhook Example Image - File Unmatched](https://i.imgur.com/sAMNiHK.png)

_File matched_  
![Webhook Example Image - File Matched](https://i.imgur.com/8okNUrL.png)

# Installation instructions

1. Download `WebhookDump.dll` from the latest release (or follow the build instructions to create this)
2. Find the install directory for [Shoko Server](https://github.com/ShokoAnime/ShokoServer/). (On windows this is likely
   `C:\Program Files (x86)\Shoko\Shoko Server\`)
3. Copy the aforementioned dll into the `plugins` directory found in the Shoko Server install directory
4. Relaunch Shoko Server (this creates the settings file mentioned below).
5. Proceed to configure the plugin as below
6. Once configured, relaunch Shoko Server once again

## Plugin setup

Everything in the plugin is configured using a JSON file, `WebhookDump.json`, which is created in the same directory as
`server-settings.json` - By default on Windows, `C:\ProgramData\ShokoServer\`. Unfortunately there's not currently no
easier way of changing the settings, but who knows what the future holds?

The default settings file that will be created is as per the below (albeit without the comments or extra spaces)

```json
{
  "Shoko": {
    // The Shoko API key, obtained as per the below instructions. Make sure the value is surrounded by quotation marks!
    "ApiKey": null,

    // The server port that Shoko runs on... You'll know if this needs changing.
    "ServerPort": 8111,

    // This is used as the base of the public address for Shoko provided to webhooks
    "PublicUrl": "http://localhost",

    // If set, this will be the port used for the public address provided to webhooks.
    // If not running behind a reverse proxy, this should probably be set to 8111, like the ServerPort.
    "PublicPort": null,

    "AutomaticMatch": {
      // This enables the 'Queued on future unmatched events' automatic rescan feature.
      "Enabled": true,

      // This controls the maximum number of times the 'Queued on future unmatched events' rescan feature will be attempted.
      "MaxAttempts": 5,

      // If set to true, the 'Watched webhook messages' feature will be enabled
      "WatchReactions": false
    }
  },
  "Webhook": {
    // When true, the webhook feature is enabled.
    "Enabled": false,

    // This is the URL for the webhook - as per https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks
    "Url": null,

    // Customisable username for the webhook
    "Username": "Shoko",

    // Customisable avatar for the webhook
    "AvatarUrl": "https://shokoanime.com/icon.png",

    // Controls how the message sent for the unmatched event is updated after being matched.
    "Matched": {
      // The discord message text (appears before the embed)
      "MessageText": null,

      // The text contents of the updated embed.
      "EmbedText": "An unmatched file automatically dumped by this plugin has now been matched.",

      // The colour for the updated embed (in hexadecimal format)
      "EmbedColor": "#57F287"
    },

    // Controls the message sent after an unmatched event.
    "Unmatched": {
      // The discord message text (appears before the embed)
      "MessageText": null,

      // The text contents of the updated embed.
      "EmbedText": "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",

      // The colour for the updated embed (in hexadecimal format)
      "EmbedColor": "#3B82F6"
    }
  }
}
```

## Getting the API key

There's a hard way, and an easy way to get this. I'd recommend the easy cross-platform way, browsing to
`http://{your-shoko-ip-here}:8111/swagger/index.html`. From here you can select the first option, `/api/auth`, and
choose to `Try it out`. No awards are provided for submitting this correctly, but you do get the required API key to
move forwards.

For those that don't like taking the easy road in life, the command line instructions on how to get an API key are as
below.

### Windows (Powershell)

```ps
$body = @{
  "user" = "shoko_username"
  "pass" = $(Read-Host -Prompt "Shoko password:... ")
  "device" = "webhook"
} | ConvertTo-Json

$headers = @{
  "accept" = "*/*"
  "Content-Type" = "application/json-patch+json"
}

((Invoke-WebRequest -Uri 'http://localhost:8111/api/auth' -Method 'POST' -Headers $headers -Body $body).Content | ConvertFrom-Json).apikey
```

### The not Windows ones

You guys have it easier here...

```bash
read -s -p "Shoko password: " password
curl -X 'POST' \
  'http://localhost:8111/api/auth' \
  -H 'accept: */*' \
  -H 'Content-Type: application/json-patch+json' \
  -d '{
  "user": "shoko_username",
  "pass": "'"$password"'",
  "device": "webhook"
}'
```

# Build instructions

1. Clone this repository & ensure that at least v6.0 of the .NET Core SDK is installed
2. Leave a like, subscribe and smash that bell button
3. Run the below commands

```sh
dotnet restore
dotnet build -c Release
```

4. Nice and easy, you can find `WebhookDump.dll` in the `bin/Release/net6.0/` folder.

## A final note

Don't trust the `.vscode` directory. It's a scary folder that's proof of my lazyness. Can be adapted however for your
own use case if you fancy making this plugin ~~less crap~~ more refined.
