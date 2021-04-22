@echo off

rem Copies the *.proto files from the olca-modules project to this project
rem assuming that this project is located next to the olca-modules project.

set script_home=%~dp0
for %%G in (%script_home%\*.proto) do (
    del %%G
)

set mods_proto= %script_home%..\..\olca-modules\olca-proto\src\main\proto
if exist %mods_proto% (
    xcopy /y %mods_proto% %script_home%
)

echo add the following includes:
for %%G in (*.proto) do (
    echo     ^<Protobuf Include="Proto\%%G" GrpcServices="Client" ProtoRoot="Proto\" /^>
)