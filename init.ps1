# ml-agents release21のクローン
Write-Host "ml-agents release21のクローンを行います。"
git clone --branch develop https://github.com/Unity-Technologies/ml-agents.git

Write-Host "プロジェクトの依存関係をインストールします。"
#rye sync 
rye sync