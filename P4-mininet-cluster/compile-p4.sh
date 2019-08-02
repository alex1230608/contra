
if [ $# -eq 2 -a "$1" == "--output" ]; then
    cd $2
else
    echo 'use default p4 file path: ./output/app'
    cd ./output/app
fi

mkdir build
cp *.p4 build/
cp *-commands.txt build/
cp attributes.txt build/
cd build
for f in *.p4; 
do 
    p4c-bm2-ss --p4v 16 $f -o $f.json
done
