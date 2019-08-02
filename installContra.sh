#!/bin/sh

sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
sudo apt install -y apt-transport-https
echo "deb https://download.mono-project.com/repo/ubuntu stable-xenial main" | sudo tee /etc/apt/sources.list.d/mono-official-stable.list
sudo apt update

sudo apt install -y mono-devel
sudo apt-get install -y fsharp
sudo apt install -y nuget

nuget restore hula.sln  # only run this when it's your first time to run Contra
xbuild hula.sln
echo 'alias hula="mono build/bin/Release/hula.exe"' >> ~/.bashrc
