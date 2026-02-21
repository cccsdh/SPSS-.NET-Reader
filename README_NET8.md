This repository contains a `.net` folder with .NET 8 SDK-style projects and a root `SpssNet.sln` solution.

Workflow (use only the .NET 8 solution)

- From the repository root run PowerShell:
  ```powershell
  ./build_net8.ps1
  ```
  or
  ```powershell
  ./build_net8.ps1 -SolutionPath .\SpssNet.sln -Configuration Release
  ```

- From a Unix shell run:
  ```bash
  ./build_net8.sh
  ```

Notes

- The .NET Framework projects in the repository are not used by this workflow.
- The build scripts call `dotnet restore` and `dotnet build` on the root `SpssNet.sln` which references the projects under `.net/`.
- Ensure you have .NET 8 SDK installed before running the scripts.
