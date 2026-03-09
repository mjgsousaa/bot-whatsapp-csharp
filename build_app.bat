@echo off
echo ===================================================
echo COMPILANDO BOTZAP AI PARA DISTRIBUICAO WIN-X64
echo ===================================================
echo.
echo Limpando builds anteriores...
dotnet clean

echo.
echo Publicando o aplicativo em modo Release...
echo (Isso gerara um executavel independente que nao precisa que o cliente tenha o .NET instalado)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

echo.
echo ===================================================
echo COMPILACAO CONCLUIDA COM SUCESSO!
echo ===================================================
echo Os arquivos finais estao na pasta:
echo bin\Release\net10.0-windows\win-x64\publish\
echo.
echo ===================================================
echo PROXIMO PASSO PARA GERAR O INSTALADOR:
echo ===================================================
echo 1. Abra o arquivo 'BotZapAI.iss' usando o programa Inno Setup Compiler.
echo 2. Pressione a tecla F9 (ou va em Build -^> Compile).
echo 3. O instalador '.exe' limpo estara na pasta 'InstaladorBuild' que sera criada.
echo.
pause
