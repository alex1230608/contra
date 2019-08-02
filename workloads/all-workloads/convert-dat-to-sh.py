import argparse
import os, shutil

parser=argparse.ArgumentParser(description="traff gen scripts")

parser.add_argument('--indir',dest="indir",default=None,help="provide director name")
parser.add_argument('--kary',dest="kary",default=2,help="k value")

args=parser.parse_args()
print args
kary=int(args.kary)

numtorhosts=kary/2
numtors=kary*(kary/2)
numhosts = kary*(kary/2)*(kary/2)
portnum=str(9000)

src_hostips=[]
dst_hostips=[]
start=kary+(kary*kary)

for i in range(1,(numtors/2)+1):
	for h in range(21,21+numtorhosts):
		src_hostips.append('10.0.'+str(i)+'.'+str(h))

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
			shfile=open(outdirf+'/'+'h'+str(start+f1_src+1)+'.sh','w')

		if(f1_time < f2_time):
			cmd='python Client.py '+dst_hostips[f1_dst]+' '+portnum+' '+str(f1_size)
			shfile.write(cmd+'\n')
			shfile.write('sleep '+str(f2_time-f1_time)+'\n')
		elif(f1_time > f2_time):
			shfile.close()
			shfile=open(outdirf+'/'+'h'+str(start+f2_src+1)+'.sh','w')
		i+=1
	shfile.close()

	for i in range(numhosts/2,numhosts):
		host=dst_hostips[i-(numhosts/2)]
		shfile=open(outdirf+'/'+'h'+str(start+i+1)+'.sh','w')
		cmd='python Server.py '+host+' '+portnum
		shfile.write(cmd+'\n')







	






