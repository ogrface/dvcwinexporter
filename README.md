# dvcwinexporter
Microsoft Dataverse solution exporter

A very simple, small Windows forms application that will allow users to connect to a Dataverse instance, see all of their unmanaged solutions and export both the managed and unmanaged zip files. It will also extract the unmanaged zip file into an Export folder using the Dataverse pac tools to prepare files to be submitted for a code review and checked into a repo.

# To build:
Simply run `dotnet build` on the DVCWinExporter.sln file.

# To run:
Run `dotnet run` on the DVCWinExporter.sln file or find the EXE in the bin folder and double click it.
