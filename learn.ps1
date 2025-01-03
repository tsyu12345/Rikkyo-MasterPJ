param(
    [int]$envs= 4,
    [string]$modelId="0",
    [bool]$force = $false,
    [bool]$exe = $false,
    [bool]$debug = $false
)

$configurePath = "Master-Simulator\config\test1_0_nav.yaml"
$exePath = "Master-Simulator\build\Master-Simulator.exe"

if($exe) {
    if($force) {
        mlagents-learn $configurePath --env=$exePath --num-envs=$envs --run-id=$modelId --force --width=1920 --height=1080 --torch-device="cuda" --debug
    } else {
        mlagents-learn $configurePath --env=$exePath --run-id=$modelId --num-envs=$envs --width=1920 --height=1080 --torch-device="cuda" --resume --debug
    }
} else {
    if($force) {
        mlagents-learn $configurePath --run-id=$modelId --force --width=1920 --height=1080 --torch-device="cuda" --debug
    } else {
        mlagents-learn $configurePath --run-id=$modelId --width=1920 --height=1080 --torch-device="cuda" --resume --debug
    }
}
