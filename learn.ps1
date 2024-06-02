param(
    [int]$envs= 4,
    [int]$modelId=0,
    [bool]$force = $false
)

$configurePath = "Master-Simulator\config\test1_0.yaml"

if($force) {
    mlagents-learn $configurePath --run-id=$modelId --force
} else {
    mlagents-learn $configurePath --run-id=$modelId
}