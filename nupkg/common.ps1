# Paths
$packFolder = (Get-Item -Path "./" -Verbose).FullName
$rootFolder = Join-Path $packFolder "../"

# List of solutions
$solutions = (
    "basics"
)

# List of projects
$projects = (
    "src/FlowMaker",
    "src/UI/FlowMaker.Avalonia",
    "src/UI/FlowMaker.UIBase",
    "src/UI/FlowMaker.WPF",
    "tools/FlowMaker.SourceGenerator"
)
