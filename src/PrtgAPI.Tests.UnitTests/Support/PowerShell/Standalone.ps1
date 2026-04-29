. $PSScriptRoot\UnitTest.ps1

function Describe($name, $script)
{
    $canRun = $true

    try
    {
        . $PSScriptRoot\Init.ps1
        InitializeUnitTestModules
    }
    catch
    {
        $canRun = $false
    }

    Pester\Describe $name -Skip:(-not $canRun) {

        if(-not $canRun)
        {
            return
        }

        BeforeAll {
            . $PSScriptRoot\Init.ps1
            InitializeUnitTestModules
        }

        & $script
    }
}