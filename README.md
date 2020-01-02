Overview
====
Contra is a language taking high-level policy as input and generating distributed switch programs which follow the path constraints and performance preference.

This repository includes all the things you need to run mininet emulation with Contra.

*Note:* Instead of cloning this repository, please just download the `P4-mininet-cluster/packages.sh` and `P4-mininet-cluster/cloudLabSetup.py` scripts and follow the following instructions to setup the environment with them automatically which includes the cloning of this repository.

Environment Setup
====
1. Use the `cloudLabSetup.py` to construct the cluster in CloudLab
2. Append public keys of all nodes to each others' `~/.ssh/authorized_keys`
	- Use `cssh` to control all of them at once, and create the ssh key pair
		```
		ssh-keygen -t rsa
		```
	- Back to your own machine, collect all public keys (in this Readme, we assumed the nodes are named as `CL*` in `~/.ssh/config` of your own machine)
		```
		ssh CL00 cat .ssh/id_rsa.pub >> tmp.tmp; \
		ssh CL01 cat .ssh/id_rsa.pub >> tmp.tmp; \
		...
		ssh CL20 cat .ssh/id_rsa.pub >> tmp.tmp
		```
	- Append these public keys to all machines. On your own machine:
		```
		cat tmp.tmp | ssh CL00 "cat >> .ssh/authorized_keys"; \
		cat tmp.tmp | ssh CL01 "cat >> .ssh/authorized_keys"; \
		...
		cat tmp.tmp | ssh CL20 "cat >> .ssh/authorized_keys"
		```
3. Copy the `P4-mininet-cluster/packages.sh` file to all machine
	```
	scp packages.sh CL00: ; \
	scp packages.sh CL01: ; \
	...
	scp packages.sh CL20:
	```
4. Run `packages.sh` on each machine separately (at least 10 secs away to avoid connection fail)

*Note:* You may want to run `sudo apt-get update` before running `packages.sh`

*Note:* Since the switch and host programs are distributed across the emulated network and therefore may be placed in different cluster nodes (according to the `p4app.json`), please make sure the corresponding input and output files, and the codes are deployed in the right place. For example, to make the p4 json files and many other related files in `build` folder available to switches on cluster node 1, you need to compile the p4 programs on node 1 instead of only compiling the programs on the master node. Same goes for workload files, too. **One easy way** to make sure everything is ready before running the program is to do every command below on every cluster node (should be easy with `cssh` tool), but only run the `run.sh` on the master.

Build Contra compiler
====
Under the repo directory on each cluster node (Use `cssh` to control all of them at once)
1. Install Contra's dependency and compile Contra compiler: 
	```
	./installContra.sh
	```

*Note:* You may want to re-login to make the alias on hula command take effect

Workload
====
We have generated several workloads (`P4-mininet-cluster/*_Conga_*/`) and converted them to the format (a bunch of shell scripts) we used in mininet scripts (`P4-mininet-cluster/*_Conga_*_out/`).
The calling of these workload scripts are currently hardcoded in the compiler codes so that the compiler will generate corresponding commands in `p4app.json` to initiate traffic in mininet.

Currently, the used workload scripts are `DCTCP_Conga_50Mbps_oneDir_Abilene_4_4_newSel_out/DCTCP_CDF_PER_FLOW_UNTIL_90_150.00_4_4_1.dat/`
To use different workloads for other setups, please modify the Contra compiler codes (`src/CodeGen.fs`) and recompile the compiler with `xbuild hula.sln`

Compile Policy and Topo with Contra compiler
====
1. In addition to the `policy` and `topo` arguments, we also have to specify `topotype` in order to tell the compiler how to generate the switch/host placement in mininet cluster and the traffic commands. This `topotype` is only needed in this mininet emulation experiment. In practice, we don't need to tell the compiler anything about the type of topology. To compile policy and topo into P4 program, under the repo directory on each cluster node:
	```
	hula --policy <policy file> --topo <topo file> --topotype <ABILENE/FATTREE>
	```
	ex. `hula --policy examples/util/util.hula --topo realDataTest/xmlData/Abilene.xml.edge --topotype ABILENE --output ./P4-mininet-cluster/abilene/`
	
	The p4 programs and other necessary files (including workloads and probes applications) for bmv2 and mininet will be in `./P4-mininet-cluster/abilene/app/`
	
	*Note:* All related commands in the following instructions are using the above `abilene` example.

2. Compile P4 program into json for simple\_switch: In `P4-mininet-cluster/`:
	```
	./compile-p4.sh --output ./abilene/app/
	```

Run the Emulation
====
1. **Only on the first cluster node (CL00)**, change directory to `P4-mininet-cluster/abilene/app/` and run:
	```
	./run.sh
	```
	Then, the emulation is run!

*Note:* In case some mininet jobs were not correctly finished last time, please use the following commands to clear the environment before running
```
sudo mn -c
sudo pkill simple_switch
sudo pkill sudo  				# if no other sudo commands were run
sudo rm /tmp/bm*
sudo rm app/build/logs/*.log    # in abilene
sudo rm app/build/*.pcap		# in abilene
```

Check the FCT output
====
The output will be on the machines which hold the end hosts (according to the host/switch placement in the `p4app.json` file generated by the compiler based on the `topotype`)
They will have names like `abilene/app/build/h*.log`

