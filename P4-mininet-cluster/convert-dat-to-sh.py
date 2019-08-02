import argparse
import os, shutil, stat

parser=argparse.ArgumentParser(description="traff gen scripts")

parser.add_argument('--indir',dest="indir",default=None,help="provide director name")
parser.add_argument('--numtorhosts',dest="numtorhosts",default=1,help="Number of hosts per ToR (or number of hosts per edge switch)")
parser.add_argument('--numtors',dest="numtors",default=2,help="Number of ToRs (or number of edge switches sending/receiving traffic)")
parser.add_argument('--numhosts',dest="numhosts",default=2,help="Number of hosts sending/receiving traffic")
parser.add_argument('--start',dest="start",default=5,help="Number of switches")
parser.add_argument('--src_mapping', dest="src_mapping", default=None, help="mapping from consecutive number to node id for source nodes")
parser.add_argument('--dst_mapping', dest="dst_mapping", default=None, help="mapping from consecutive number to node id for destination nodes")

args=parser.parse_args()
print args

#numtorhosts=kary/2
#numtors=kary*(kary/2)
#numhosts = kary*(kary/2)*(kary/2)
numtorhosts=int(args.numtorhosts)
numtors=int(args.numtors)
numhosts=int(args.numhosts)
portnum=str(9000)
if args.src_mapping != None:
	src_nodes = args.src_mapping.split(',')
if args.dst_mapping != None:
	dst_nodes = args.dst_mapping.split(',')

src_hostips=[]
dst_hostips=[]
#start=(kary*kary)/4+(kary*kary)
start=int(args.start)

if args.src_mapping != None:
	for i in range(0, len(src_nodes)):
#			index = numtorhosts*(i-1)+(h-21)
			print src_nodes[i]
			src_hostips.append('10.0.'+str((int(src_nodes[i])-1)/numtorhosts+1)+'.'+str((int(src_nodes[i])-1)%numtorhosts+21))
else:
	for i in range(1,(numtors/2)+1):
		for h in range(21,21+numtorhosts):
			src_hostips.append('10.0.'+str(i)+'.'+str(h))
if args.dst_mapping != None:
	for i in range(0, len(dst_nodes)):
#			index = numtorhosts*(i-(numtors/2)-1)+h-21
			print dst_nodes[i]
			dst_hostips.append('10.0.'+str((int(dst_nodes[i])-1)/numtorhosts+1)+'.'+str((int(dst_nodes[i])-1)%numtorhosts+21))
else:
	for i in range((numtors/2)+1, numtors+1):
		for h in range(21,21+numtorhosts):
			dst_hostips.append('10.0.'+str(i)+'.'+str(h))

print src_hostips
print dst_hostips
outdir=args.indir+'_out'
indir = args.indir
if os.path.isdir(outdir):
	shutil.rmtree(outdir)

for filename in os.listdir(indir):
	outdirf=outdir+'/'+filename
	print outdirf
	os.makedirs(outdirf)

	with open(indir+'/'+filename) as f:
		ns3_dat=f.readlines()

	ns3_dat=[x.strip() for x in ns3_dat]

	i=0
	shfile=None
	while i < len(ns3_dat)-1:
		f1=ns3_dat[i].split(' ')
		f1_time=float(f1[0])
		f1_src = int(f1[1])
		f1_dst = int(f1[2])
		f1_size = float(f1[3])
		f2=ns3_dat[i+1].split(' ')
		f2_time=float(f2[0])
		f2_src = int(f2[1])
		f2_dst = int(f2[2])
		f2_size = float(f2[3])
		if(i==0):
			node_id = str(int(src_nodes[f1_src])+start) if args.src_mapping != None else str(start+f1_src+1)
			shfile=open(outdirf+'/'+'h'+node_id+'.sh','w')
			os.chmod(outdirf+'/'+'h'+node_id+'.sh', stat.S_IRWXU|stat.S_IRWXG|stat.S_IROTH)
			shfile.write('( sleep '+str(f1_time)+' ; ')
		if(f1_time < f2_time):
			cmd='python ../../../Client.py '+dst_hostips[f1_dst]+' '+portnum+' '+str(f1_size)
			shfile.write(cmd+' ) &\n')
			shfile.write('( sleep '+str(f2_time)+' ; ')
		elif(f1_time > f2_time):
			cmd='python ../../../Client.py '+dst_hostips[f1_dst]+' '+portnum+' '+str(f1_size)
			shfile.write(cmd+' ) &\n')
			shfile.close()
			node_id = str(int(src_nodes[f2_src])+start) if args.src_mapping != None else str(start+f2_src+1)
			shfile=open(outdirf+'/'+'h'+node_id+'.sh','w')
			os.chmod(outdirf+'/'+'h'+node_id+'.sh', stat.S_IRWXU|stat.S_IRWXG|stat.S_IROTH)
			shfile.write('( sleep '+str(f2_time)+' ; ')
		i+=1
	cmd='python ../../../Client.py '+dst_hostips[f2_dst]+' '+portnum+' '+str(f2_size)
	shfile.write(cmd+' ) &\n')
	shfile.close()

	for i in range(numhosts/2,numhosts):
		host=dst_hostips[i-(numhosts/2)]
		node_id = str(int(dst_nodes[i-(numhosts/2)])+start) if args.dst_mapping != None else str(start+i+1)
		shfile=open(outdirf+'/'+'h'+node_id+'.sh','w')
		os.chmod(outdirf+'/'+'h'+node_id+'.sh', stat.S_IRWXU|stat.S_IRWXG|stat.S_IROTH)
		cmd='python ../../../Server.py '+host+' '+portnum
		shfile.write(cmd+'\n')





	






