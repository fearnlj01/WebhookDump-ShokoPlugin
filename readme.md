# What is this?
~~Badly made, hacked together and published to GitHub far too soon...~~  
Not great, but functional :) just don't look at the commit history.

The features this [Shoko](https://shokoanime.com/) plugin offers are outlined as below:
- Automatically AVDumps a file the first time it is not matched against AniDB by Shoko
- Sends an embed to Discord via a webhook, with easy access to the ED2K for the unrecognised file and a link to the (currently unstable) WebUI's unrecognised utilities page. Somehow, this ends up being easier to copy/paste from mobile than on desktop!

# Installation instructions
1) Download `WebhookDump.dll` from the latest release (or follow the build instructions to create this)
2) Find the install directory for [Shoko Server](https://github.com/ShokoAnime/ShokoServer/). (On windows this is likely `C:\Program Files (x86)\Shoko\Shoko Server\`)
3) Copy the aforementioned dll into the `plugins` directory found in the Shoko Server install directory
## Plugin setup
Everything in the plugin is currently configured via environment variables. For full functionality, the below variables must all be set.
 - `SHOKO_DISCORD_WEBHOOK_APIKEY`  |  See [here](#getting-the-api-key) for how to obtain this.
 - `SHOKO_DISCORD_WEBHOOK_URL`  |  See [here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks) for how to get this from Discord.
 - `SHOKO_DISCORD_WEBHOOK_AVATAR_URL`  |  Just a nice little image url (try: [this](https://shokoanime.com/icon.png) for a start)
 - `SHOKO_DISCORD_WEBHOOK_SHOKO_URL`  |  The start of the shoko URL/IP - examples as below.
	- `https://domain.com`
	- `http://localhost`
	- `http://10.0.0.10`
	- `https://shoko.domain.shop/`

### The optional enviroment variable...
For all you weird folk out there that have decided to change the default port that Shoko Server runs on...
 - `SHOKO_DISCORD_WEBHOOK_SHOKO_PORT` | e.g. 811*2*


## Getting the API key
Make sure to change the username & ip address/domain of the server as applicable!

There's also an easy way, where if you go to `http://{your-shoko-ip-here}:8111/swagger/index.html` you can click on the first option, `/api/auth`, choose to `Try it out` and interact with the web interface this way to get the API key.

We like things the hard way however, so there's the far more verbose instructions available below

### Windows (Powershell)
```ps
$body = @{
  "user" = "shoko_username"
  "pass" = $(Read-Host -Prompt "Shoko password:... ")
  "device" = "not_webhook"
} | ConvertTo-Json

$headers = @{
  "accept" = "*/*"
  "Content-Type" = "application/json-patch+json"
}

((Invoke-WebRequest -Uri 'http://localhost:8111/api/auth' -Method 'POST' -Headers $headers -Body $body).Content | ConvertFrom-Json).apikey
```
### Operatings systems that shall not be named
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