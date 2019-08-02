import json
import argparse

parser = argparse.ArgumentParser(description='Read p4app.json')
parser.add_argument('--manifest', '-m', help='Path to manifest file',
                    type=str, action="store", required=True)

args = parser.parse_args()

with open(args.manifest, 'r') as f:
	manifest = json.load(f)

links = manifest['targets']['multiswitch']['links']

for i in range(0, len(links)):
	if links[i][0][0] == 's' and links[i][1][0] == 's':
		print 'Edge('+str(int(links[i][0][1:])-1)+', '+str(int(links[i][1][1:])-1)+'),'
