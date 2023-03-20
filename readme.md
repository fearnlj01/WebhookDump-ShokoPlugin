# What is this?
Badly made, hacked together and published to GitHub far too soon...

Unfortunately, it's also functional so there is some utility in this... The features this [Shoko](https://shokoanime.com/) plugin offers are outlined as below:
- Automatically AVDumps a file the first time it is not matched against AniDB by Shoko
- Sends an embed to Discord via a webhook, with easy access to the ED2K for the unrecognised file and a link to the (currently unstable) WebUI's unrecognised utilities page

# How do I get the webhook working?
For now, the limited options that are avilable are set via environment variables! There's better ways of doing this, but this was quicker and easier than others. Maybe I should've asked ChatGPT for a better way.

The following environment variables will need to be set for the Discord Webhook to work:
- `SHOKO_DISCORD_WEBHOOK_URL`
	-	e.g. `https://discord.com/api/webhooks/{discordChannelID}/{discordWebhookToken}`
- `SHOKO_DISCORD_WEBHOOK_AVATAR_URL`
	-	e.g. `https://shokoanime.com/icon.png`
- `SHOKO_DISCORD_WEBHOOK_SHOKO_URL`
	-	e.g. `https://domain.com:8111`, `http://10.0.0.2:8111` or even `https://shoko.domain.com` for those with no sense of security

# Build instructions
1.	Clone this repository, including it's submodules (i.e. `git clone --recurse-submodules .....`)
2.	Ensuring that you have the .NET Core SDK installed (v6.0+), run the following commands
```sh
$ dotnet restore Plugin/WebhookDump.csproj
$ dotnet publish -c Release Plugin/WebhookDump.csproj
```
3.	Ignoring the fact that you just had to build the entirety of Shoko Server for this, from the output folder, `Plugin/bin/Release/net6.0/`, copy `WebhookDump.dll` to the Shoko plugin directory (found in the install location of Shoko Server).

## Notes
If you already have a copy of the `Shoko Server` source on your computer, you can skip re-obtaining it as a submodule. Just need to change the `ProjectReference` location in `WebhookDump.csproj` as appropraite before building this plugin and you should be good to go.

## Bonus for VS Code users on Windows
Life gets a little easier as the `.vscode` directory in this repository contains a few things to make building and copying to Shoko Server marginally quicker. The default build task will build the plugin and save it into the plugin folder given a default Windows Shoko Server setup.