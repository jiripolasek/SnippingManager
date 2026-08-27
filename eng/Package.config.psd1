@{
    RepositoryRoot = '..'

    AppProjectPath      = 'src\JPSoftworks.ScreenManExtension\JPSoftworks.ScreenManExtension.csproj'
    WapProjectPath      = 'src\JPSoftworks.ScreenManExtension.Package\JPSoftworks.ScreenManExtension.Package.wapproj'
    PackageManifestPath = 'src\JPSoftworks.ScreenManExtension.Package\Package.appxmanifest'
    ArtifactsPath       = 'artifacts'

    PackageExecutablePath = 'JPSoftworks.ScreenManExtension\JPSoftworks.ScreenManExtension.exe'

    DefaultConfiguration = 'Release'
    DefaultPlatform      = 'x64'

    RuntimeIdentifiers = @{
        x64   = 'win-x64'
        ARM64 = 'win-arm64'
    }

    Host = @{
        ProcessName = 'Microsoft.CmdPal.UI'
        LaunchUri   = 'x-cmdpal://'
        ReloadUri   = 'x-cmdpal://reload'
    }
}
