# What is this?
~~Badly made, hacked together and published to GitHub far too soon...~~  
Not great, but functional :) just don't look at the commit history.

The features this [Shoko](https://shokoanime.com/) plugin offers are outlined as below:
- Automatically AVDumps a file the first time it is not matched against AniDB by Shoko
- Sends an embed to Discord via a webhook (as pictured below). This contains:
  - The dumped ED2K for the unrecognised file
  - In the embed title, a link to the (currently unstable) WebUI's unrecognised utilities page.
  - Links to the three most likely (based off file name) relevant 'Add release' pages on AniDB

![Webhook Example Image](https://i.imgur.com/NUvB1nJ.png)

# Installation instructions
1) Download `WebhookDump.dll` from the latest release (or follow the build instructions to create this)
2) Find the install directory for [Shoko Server](https://github.com/ShokoAnime/ShokoServer/). (On windows this is likely `C:\Program Files (x86)\Shoko\Shoko Server\`)
3) Copy the aforementioned dll into the `plugins` directory found in the Shoko Server install directory
4) Relaunch Shoko Server (this creates the settings file mentioned below).
5) Proceed to configure the plugin as below
6) Once configured, relaunch Shoko Server once again
## Plugin setup
Everything in the plugin is now configured via a JSON file, `WebhookDump.json`, that is created in the same directory as `server-settings.json` - By default on Windows, `C:\ProgramData\ShokoServer\`. Unfortunately there's not currently any easier way to modify this, but who knows what the future holds?

The default settings file that will be created is as per the below (albeit without the comments)
```json
{
  "Shoko": {
    "ApiKey": "", // Shoko API key, obtained inline with the below
    "ServerPort": 8111, // The port that Shoko Server runs on... You'll know if you need to change this
    "PublicUrl": "http://localhost", // This will be used as the 'public' address to Shoko sent in the webhooks
    "PublicPort": null // If set, this will be the port used in the 'public' address for Shoko. You likely want to set this to be 8111.
  },
  "Webhook": {
    "Url": "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}", // See https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks) for how to get this from Discord.
    "Username": "Shoko", // Username shown in the Discord Webhook
    "AvatarUrl": "https://shokoanime.com/icon.png", // Avatar used for the Discord webhook
    "MessageText": null, // Change this if you want there to be any message text before the embed
    "EmbedText": "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.", // The main text used in the embed.
    "EmbedColor": 3900150 // Forgive me for this, it was the easiest way - this is a standard hexadecimal colour, just in decimal to make the JSON gods happy...
  }
}
```

## Getting the API key
There's a hard way, and an easy way to get this. I'd recommend the easy cross-platform way, browsing to `http://{your-shoko-ip-here}:8111/swagger/index.html`. From here you can select the first option, `/api/auth`, and choose to `Try it out`. No awards are provided for submitting this correctly, but you do get the required API key to move forwards.

For those that don't like taking the easy road in life, the command line instructions on how to get an API key are as below.
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
1) Clone this repository & ensure that at least v6.0 of the .NET Core SDK is installed
2) Leave a like, subscribe and smash that bell button
3) Run the below commands
```sh
dotnet restore
dotnet build -c Release
```
4) Nice and easy, you can find `WebhookDump.dll` in the `bin/Release/net6.0/` folder.

## A final note
Don't trust the `.vscode` directory. It's a scary folder that's proof of my lazyness. Can be adapted however for your own use case if you fancy making this plugin less crap.