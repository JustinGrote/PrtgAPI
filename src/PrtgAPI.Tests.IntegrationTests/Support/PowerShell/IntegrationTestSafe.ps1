. $PSScriptRoot\Init.ps1

function Describe($name, $script) {
    $canRun = $true

    try
    {
        . $PSScriptRoot\Init.ps1
        InitializeModules "PrtgAPI.Tests.IntegrationTests" $PSScriptRoot
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

        $init = $false

        BeforeAll {
            . $PSScriptRoot\Init.ps1
            $currentName = $Pester.CurrentBlock.Name
            StartupSafe $currentName

            $init = $true

            LogTest "Running safe test '$currentName'"
        }

        AfterAll {
            . $PSScriptRoot\Init.ps1

            if($init)
            {
                $currentName = $Pester.CurrentBlock.Name
                LogTest "Completed '$currentName' tests; no need to clean up"
            }

            ClearTestName
        }

        & $script
    }
}