param(
    [int]$envs= 4,
    [int]$modelId=0,
    [bool]$force = $false
)

$configurePath = "Master-Simulator\config\model-0_1_0.yaml"

mlagents-learn $configurePath --env=$envs --run-id=$modelId --force=$force