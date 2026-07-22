#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Launch a StoryWeaver session.

.DESCRIPTION
    Convenience wrapper around `dotnet run` for the console harness.

    It runs from the repository root regardless of where you invoke it, which matters more
    than it looks: the save directory (saves/) is resolved relative to the working directory,
    so starting from somewhere else would silently create a second, empty world instead of
    resuming the one you have been playing.

    With no arguments it starts a play session. Any arguments given are passed straight
    through to the CLI instead.

.EXAMPLE
    ./play.ps1
    Play. Creates saves/marrow on first run, resumes it after that.

.EXAMPLE
    ./play.ps1 --selftest
    Offline serialization checks. No API calls, spends nothing.

.EXAMPLE
    ./play.ps1 --eval --runs 7
    Score extraction against the fixed scenarios. Spends real credits.

.EXAMPLE
    ./play.ps1 --eval --scenarios player-arrival --show-deltas
    Run named scenarios and print what the model actually proposed.
#>

$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    # No arguments means the common case: play.
    $cliArgs = if ($args.Count -gt 0) { $args } else { @('--play') }

    # Everything is splatted as one array, including the '--' separator. Written as a bare
    # token on the command line, PowerShell's parser treats '--' as end-of-parameters and
    # mangles it — the CLI then receives a stray '-' and reports it as an unreadable settings
    # file path. Inside a splatted array it is passed through literally.
    $dotnetArgs = @(
        'run'
        '--project'
        'src/StoryWeaver.Cli/StoryWeaver.Cli.csproj'
        '--verbosity'
        'quiet'
        '--'
    ) + $cliArgs

    & dotnet @dotnetArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
