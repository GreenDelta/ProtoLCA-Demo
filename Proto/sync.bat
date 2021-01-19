rem @echo off

rem Copies the *.proto files from the olca-modules project to this project
rem assuming that this project is located next to the olca-modules project.

set script_home=%~dp0
set mods_proto= %script_home%..\..\olca-modules\olca-proto\src\main\proto
if exist %mods_proto% (
    xcopy /y %mods_proto% %script_home%
)
