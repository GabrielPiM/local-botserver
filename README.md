# Local BotServer

BotServer for OTCv8 and OTC Mehah that runs on your own PC: one small `.exe` with a window, no Node and nothing to
install on Windows 7 or newer. It speaks the standard BotServer protocol (OTCv8/vBot) and is the local server that
ships with DibraBot. [Português](#português)

This repository is the complete source code of that program, so anyone can read it and build it.

## What it does on your PC

- Listens on `127.0.0.1:8000`, so only your PC can connect. The local network gets in only if you set
  `BOTSERVER_LAN=1`.
- Never connects to the internet by itself. The Discord, YouTube and GitHub buttons open your browser, and only when
  you click them.
- Writes a single file, `logs/output.log`, next to the `.exe` (character and room names, and the IP of connections
  that fail before joining a room).
- Reads an optional `config.env` next to the `.exe`.
- Does not need administrator rights, does not touch the registry and does not start with Windows. Closing the window
  stops the server.

## Use

1. Open `LocalBotServer.exe`. Keep `LocalBotServer.exe.config` next to it: without that file Windows 8 and newer ask
   to install .NET 3.5.
2. In the bot, connect to `ws://127.0.0.1:8000/` (Copy button).

Whoever joins the same room (the `channel` of `init`) talks to each other. There are no accounts or passwords.

Windows 7, 8, 10 and 11 already have the .NET it needs. Windows XP SP3 and Vista need .NET 4 or 3.5 SP1.

## config.env

Optional; model in [config.env.example](config.env.example). An unknown key or invalid value stops the program with an
error on screen.

| Key | Default | Effect |
| --- | --- | --- |
| `BOTSERVER_WS_PORT` | 8000 | port |
| `BOTSERVER_LAN` | 0 | `0` accepts only this PC; `1` accepts the network |
| `BOTSERVER_WS_MAX_PAYLOAD` | 65536 | bytes per packet |
| `BOTSERVER_WS_MAX_PACKETS` | 100 | packets per 1 to 2 second window |
| `BOTSERVER_WS_MAX_TOPIC_LENGTH` | 30 | `topic` length |
| `BOTSERVER_PING_INTERVAL_MS` | 1000 | ping interval |

## Build it yourself

You need Windows 8 or newer, which already has the C# compiler (.NET Framework 4).

1. Download the NuGet package
   [Microsoft.NETFramework.ReferenceAssemblies.net35 1.0.3](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net35/1.0.3)
   (SHA-256 `b16156111a88670d91a757fbd465fcb4856e034b1a4d523ae2c41a470f3578f9`). A `.nupkg` is a zip.
2. From `build/.NETFramework/v3.5/` copy to `refs/net35/`: `mscorlib.dll`, `System.dll`, `System.Core.dll`,
   `System.Xml.dll`, `System.Web.Extensions.dll`, `PresentationCore.dll`, `PresentationFramework.dll` and
   `WindowsBase.dll`.
3. Run `build.bat`. The result is in `bin/`.

The compiler stamps each build with its own date and identifier, so your `.exe` works the same as the distributed one
but its hash is different.

Tests (Node 22 or newer): `node tests/server_contracts.js`. The window opens during the test.

## Source

| File | What is in it |
| --- | --- |
| `src/Program.cs` | start and single instance |
| `src/Server.cs` | rooms and protocol |
| `src/WebSocket.cs` | frames, session and ping |
| `src/MainWindow.xaml`, `src/MainWindow.cs` | window |
| `src/Config.cs`, `src/Log.cs` | `config.env` and log |
| `src/TestPage.cs` | test page at `http://127.0.0.1:8000/` |

## Português

BotServer para OTCv8 e OTC Mehah que roda no seu próprio PC: um `.exe` pequeno, com janela, sem Node e sem instalar
nada no Windows 7 ou mais novo. É o servidor local que vem com o DibraBot, e este repositório é o código-fonte
completo dele.

- Atende só em `127.0.0.1:8000`: só o seu PC conecta, a não ser que você use `BOTSERVER_LAN=1`.
- Não acessa a internet por conta própria. Discord, YouTube e GitHub abrem no navegador só quando você clica.
- Grava um arquivo só, `logs/output.log`, ao lado do `.exe`.
- Não pede administrador, não mexe no registro e não inicia com o Windows. Fechar a janela encerra o servidor.

Para usar, abra `LocalBotServer.exe` (com o `LocalBotServer.exe.config` ao lado) e conecte o bot em
`ws://127.0.0.1:8000/`. Para compilar, siga [Build it yourself](#build-it-yourself).

## License

[MIT](LICENSE). Discord, YouTube and GitHub names and logos belong to their owners.
