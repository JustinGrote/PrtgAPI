<#
.SYNOPSIS
Runs PrtgAPI CI tasks in a provider-neutral way

.DESCRIPTION
Test-PrtgCI executes the same core CI tasks used by GitHub Actions and local CI simulation.
By default, Test-PrtgCI will build and run tests. This can be limited by specifying
individual tasks via the -Task parameter.

.PARAMETER Task
CI task to execute. If no value is specified, Build and Test tasks will be executed.

.PARAMETER Legacy
Specifies whether to use legacy .NET infrastructure when running CI tasks

.EXAMPLE
C:\> Simulate-PrtgCI
Run default CI tasks (Build and Test)

.EXAMPLE
C:\> Simulate-PrtgCI -Task Test
Run CI tests only
#>
function Test-PrtgCI {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [ValidateSet('Install', 'Restore', 'Build', 'Package', 'Test')]
        [string[]]$Task,

        [Parameter(Mandatory = $false)]
        [Configuration]$Configuration = 'Debug',

        [ValidateScript({
                if ($_ -and !(Test-IsWindows)) {
                    throw 'Parameter is only supported on Windows.'
                }
                return $true
            })]
        [Parameter(Mandatory = $false)]
        [switch]$Legacy
    )

    $buildFolder = Get-SolutionRoot
    $configurationName = $Configuration.ToString()
    $isCore = -not $Legacy

    if ($null -eq $Task) {
        $Task = @('Build', 'Test')
    }

    if ('Install' -in $Task -or 'Restore' -in $Task) {
        Install-CIDependency dotnet
        Invoke-Process { dotnet restore (Join-Path $buildFolder 'PrtgAPI.slnx') } -WriteHost
    }

    if ('Build' -in $Task) {
        Invoke-CIBuild -BuildFolder $buildFolder -Configuration $configurationName -IsCore:$isCore -SourceLink
    }

    if ('Package' -in $Task) {
        $outputFolder = Join-Path $buildFolder 'artifacts\packages'

        if (!(Test-Path $outputFolder)) {
            New-Item -ItemType Directory -Path $outputFolder | Out-Null
        }

        $version = (Get-CIVersion -IsCore:$isCore).Package.ToString()
        $manager = New-PackageManager

        $manager.InstallCSharpPackageSource()

        try {
            New-CSharpPackage -BuildFolder $buildFolder -OutputFolder (PackageManager -RepoLocation) -Version $version -Configuration $configurationName -IsCore:$isCore

            if ($isCore) {
                $powerShellOutput = Get-PowerShellOutputDir -BuildFolder $buildFolder -Configuration $configurationName -IsCore:$isCore
                New-PowerShellPackage -OutputDir $powerShellOutput -RepoManager $manager -Configuration $configurationName -IsCore:$isCore -Redist
            }

            Move-Packages '' $outputFolder | Out-Null
        } finally {
            $manager.UninstallCSharpPackageSource()
        }
    }

    if ('Test' -in $Task) {
        Invoke-CITest -BuildFolder $buildFolder -Configuration $configurationName -IsCore:$isCore
    }
}