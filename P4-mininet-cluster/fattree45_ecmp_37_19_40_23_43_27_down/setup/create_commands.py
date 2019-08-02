import argparse
import os, shutil
import errno

parser=argparse.ArgumentParser(description="fattree ecmp scripts")
parser.add_argument('--kary', dest="kary", default=2, help="k.value")
parser.add_argument('--outdir', dest="outdir", default=2, help="output directory")
parser.add_argument('--fail', dest="fail", default=[], action='append', help='fail links, e.g. `--fail 1,2` or `--fail "1, 2"`')

args=parser.parse_args()
print args

fail = [sorted([int(x) for x in f.split(',')]) for f in args.fail]
print fail


outdir=args.outdir
try:
	os.mkdir(outdir)
except OSError as exc:
	if exc.errno != errno.EEXIST:
		raise
	pass

kary=int(args.kary)
edge_node_start = 1
edge_node_end = kary*kary/2
aggr_node_start = edge_node_end+1
aggr_node_end = edge_node_end + kary*kary/2
core_node_start = aggr_node_end+1
core_node_end = aggr_node_end + kary*kary/4
half_k = kary/2

for l in fail:
	if l[0] < aggr_node_start or l[0] > aggr_node_end or l[1] < core_node_start or l[1] > core_node_end:
		print 'only support failing core-aggr links'
		raise

for edge in range(edge_node_start, edge_node_end+1):
	fd = open(outdir+'/s'+str(edge)+'-commands.txt', 'w')
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		if edge == dst: #forward to endhost (deterministic path)
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
		elif (dst-1)/half_k == (edge-1)/half_k: # intrapod
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k) )
		else:				# interpod
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k*half_k) )
	for path in range(0, half_k*half_k):
		port = path%half_k + 2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.0.0/16 => %d\n' % (path, port) )
	for endHost in range(21, 21+half_k):
		port = endHost-21+half_k+2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.%d/32 => %d\n' % (half_k*half_k, edge, endHost, port) )
	fd.close()
#table_add tab_set_identification set_identification 1 4 =>
#table_add tab_set_identification set_identification 1 5 =>
#table_add tab_set_identification set_identification 1 6 =>
#table_add tab_set_identification set_identification 1 7 =>
#table_add tab_set_identification set_identification 1 8 =>
#table_add tab_set_identification set_identification 1 9 =>
#table_add tab_set_identification set_identification 1 10 =>
#table_add tab_set_identification set_identification 1 11 =>

failAggr = [ f[0] for f in fail ]
failCore = [ f[1] for f in fail ]
failCoreStrideToAggr = {}
failAggrToCore = {}
failPodToCore = {}

for i in range(0, len(failCore)):
	failCoreStrideToAggr[(failCore[i]-core_node_start)/half_k] = failAggr[i]
	failAggrToCore[failAggr[i]] = failCore[i]
	failPodToCore[(failAggr[i]-aggr_node_start)/half_k] = failCore[i]

for aggr in range(aggr_node_start, aggr_node_end+1):
	fd = open(outdir+'/s'+str(aggr)+'-commands.txt', 'w')
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		if (dst-1)/half_k == (aggr-aggr_node_start)/half_k: # downstream intrapod (deterministic)
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
			port = (dst-1)%half_k + 2
			fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.0/24 => %d\n' % (half_k*half_k, dst, port) )
		else:							# upstream interpod
			if aggr in failAggr:
				fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k-1) )
			elif (aggr-1)%half_k in failCoreStrideToAggr:
				failPod = (failCoreStrideToAggr[(aggr-1)%half_k]-aggr_node_start)/half_k
				print '%d, %d' % (aggr, failPod)
				if (dst-edge_node_start)/half_k == failPod:
					fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k-1) )
				else:
					fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k*half_k) )
			else:
				fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k*half_k) )
	if aggr in failAggr:
		path = 0
		for port in range(2+half_k, 2+kary):
			if (failAggrToCore[aggr]-1)%half_k + 2 + half_k != port:
				fd.write('table_add ecmp_nhop set_nhop %d 10.0.0.0/16 => %d\n' % (path, port) )
				path = path+1
	else:
		if (aggr-1)%half_k in failCoreStrideToAggr:
			path = 0
			failPod = (failCoreStrideToAggr[(aggr-1)%half_k]-aggr_node_start)/half_k
			print 'failPod: %d' % failPod
			for dst in range(edge_node_start+failPod*half_k, edge_node_start+failPod*half_k+half_k):
				path = 0
				print dst
				for port in range(2+half_k, 2+kary):
					if (failPodToCore[failPod]-1)%half_k + 2 + half_k != port:
						fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.0/24 => %d\n' % (path, dst, port) )
						path = path+1

		for path in range(0, half_k*half_k):
			port = path/half_k + half_k + 2
			fd.write('table_add ecmp_nhop set_nhop %d 10.0.0.0/16 => %d\n' % (path, port) )
	fd.close()

for core in range(core_node_start, core_node_end+1):
	fd = open(outdir+'/s'+str(core)+'-commands.txt', 'w')
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		#always deterministic
		fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
		port = (dst-1)/half_k + 2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.0/24 => %d\n' % (half_k*half_k, dst, port) )
	fd.close()

