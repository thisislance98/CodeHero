@echo off
setlocal enabledelayedexpansion

:: Unity Chat Window CLI Helper Script for Windows
:: This script provides an easy way to send messages to the Unity ChatWindow from the command line

:: Default Unity path (adjust as needed for your system)
set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.10f1\Editor\Unity.exe"

:: Get the current directory as the project path
set "PROJECT_PATH=%CD%"

:: Parse command line arguments
set "COMMAND="
set "MESSAGE="

:parse_args
if "%~1"=="" goto end_parse
if "%~1"=="-u" (
    set "UNITY_PATH=%~2"
    shift
    shift
    goto parse_args
)
if "%~1"=="--unity-path" (
    set "UNITY_PATH=%~2"
    shift
    shift
    goto parse_args
)
if "%~1"=="-p" (
    set "PROJECT_PATH=%~2"
    shift
    shift
    goto parse_args
)
if "%~1"=="--project" (
    set "PROJECT_PATH=%~2"
    shift
    shift
    goto parse_args
)
if "%~1"=="-h" goto show_help
if "%~1"=="--help" goto show_help
if "%~1"=="send" (
    set "COMMAND=send"
    set "MESSAGE=%~2"
    shift
    shift
    goto parse_args
)
if "%~1"=="response" (
    set "COMMAND=response"
    shift
    goto parse_args
)
if "%~1"=="clear" (
    set "COMMAND=clear"
    shift
    goto parse_args
)
echo Unknown option: %~1
goto show_help

:end_parse

:: Check if Unity path exists
if not exist "%UNITY_PATH%" (
    echo Error: Unity not found at %UNITY_PATH%
    echo Please specify the correct Unity path using -u option or edit the script.
    echo.
    echo Common Unity paths:
    echo   Windows: C:\Program Files\Unity\Hub\Editor\[VERSION]\Editor\Unity.exe
    echo   Windows (Custom): D:\Unity\Hub\Editor\[VERSION]\Editor\Unity.exe
    exit /b 1
)

:: Check if project path exists
if not exist "%PROJECT_PATH%" (
    echo Error: Project path not found: %PROJECT_PATH%
    exit /b 1
)

:: Execute command
if "%COMMAND%"=="send" goto send_message
if "%COMMAND%"=="response" goto get_response
if "%COMMAND%"=="clear" goto clear_messages
goto show_help

:send_message
if "%MESSAGE%"=="" (
    echo Error: No message provided
    echo Usage: chat_cli.bat send "Your message here"
    exit /b 1
)

echo Sending message to ChatWindow: %MESSAGE%
echo Unity: %UNITY_PATH%
echo Project: %PROJECT_PATH%
echo.

"%UNITY_PATH%" -batchmode -quit -projectPath "%PROJECT_PATH%" -executeMethod ChatWindowCLI.SendMessage -message "%MESSAGE%" -logFile - 2>&1 | findstr /C:"ChatWindowCLI" /C:"Error" /C:"Exception"

echo.
echo Message sent! If Unity is running, the message should appear in the ChatWindow.
goto end

:get_response
echo Getting last CLI response...
"%UNITY_PATH%" -batchmode -quit -projectPath "%PROJECT_PATH%" -executeMethod ChatWindowCLI.GetLastResponse -logFile - 2>&1 | findstr /C:"ChatWindowCLI" /C:"Error" /C:"Exception"
goto end

:clear_messages
echo Clearing CLI messages...
"%UNITY_PATH%" -batchmode -quit -projectPath "%PROJECT_PATH%" -executeMethod ChatWindowCLI.ClearMessages -logFile - 2>&1 | findstr /C:"ChatWindowCLI" /C:"Error" /C:"Exception"
goto end

:show_help
echo Unity ChatWindow CLI Helper
echo.
echo Usage:
echo   chat_cli.bat send "Your message here"
echo   chat_cli.bat response
echo   chat_cli.bat clear
echo.
echo Commands:
echo   send ^<message^>    Send a message to the ChatWindow
echo   response          Get the last CLI response
echo   clear             Clear any pending CLI messages
echo.
echo Options:
echo   -u, --unity-path  Specify Unity executable path
echo   -p, --project     Specify project path (default: current directory)
echo   -h, --help        Show this help message
echo.
echo Examples:
echo   chat_cli.bat send "Create a player movement script"
echo   chat_cli.bat send "What scripts are in my project?"
echo   chat_cli.bat -u "C:\Unity\Editor\Unity.exe" send "Hello Claude!"

:end 