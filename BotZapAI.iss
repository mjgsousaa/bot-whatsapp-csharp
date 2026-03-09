; Script do Inno Setup para o BotZap AI

[Setup]
; INFORMAÇÕES BÁSICAS
AppId={{8A7B6C5D-4E3F-2A1B-0C9D-8E7F6A5B4C3D}
AppName=BotZap AI
AppVersion=5.0
AppPublisher=Sua Agencia ou Nome
AppPublisherURL=https://seusite.com.br
AppSupportURL=https://seusite.com.br/suporte
AppUpdatesURL=https://seusite.com.br/atualizacoes

; CAMINHOS DE INSTALAÇÃO
DefaultDirName={autopf}\BotZap AI
DisableProgramGroupPage=yes

; INTERFACE DO INSTALADOR
; Descomente e aponte para a licença se tiver uma
; LicenseFile=TERMOS_DE_USO.txt
SetupIconFile=Assets\logoCerta.ico
UninstallDisplayIcon={app}\BotWhatsappCSharp.exe

; CONFIGURAÇÕES DE COMPRESSÃO E SAÍDA
Compression=lzma2/max
SolidCompression=yes
OutputDir=InstaladorBuild
OutputBaseFilename=Instalar_BotZap_AI_v5

; REQUISITOS (Exige privilégios de administrador para instalar em Arquivos de Programas)
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; NOTA IMPORTANTE: Você deve compilar o projeto em MODO RELEASE antes de gerar este instalador.
; O comando recomendado é: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\BotZap AI"; Filename: "{app}\BotWhatsappCSharp.exe"; IconFilename: "{app}\BotWhatsappCSharp.exe"
Name: "{autodesktop}\BotZap AI"; Filename: "{app}\BotWhatsappCSharp.exe"; IconFilename: "{app}\BotWhatsappCSharp.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BotWhatsappCSharp.exe"; Description: "{cm:LaunchProgram,BotZap AI}"; Flags: nowait postinstall skipifsilent
