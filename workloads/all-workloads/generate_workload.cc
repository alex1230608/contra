#include <time.h>
#include <string.h>
#include <stdlib.h>
#include <limits.h>
#include <assert.h>
#include <math.h>
#include <random>
#include <iostream>
#include <chrono>

double input_percentage[105], input_flowsize[105];
int input_linecnt;

#define MAX_NODES 1000 //Number of senders/receivers in the network.
#define MAX_FLOWS 100000000 //Number of flows we want to generate. 
#define MIN_FLOWS 1 //If you sample too few flows, then the sampling of the distribution is HIGHLY inaccurate.
#define TRACE_LEN 10 //We generate a 1-second trace

int sender[MAX_NODES], receiver[MAX_NODES], flowsize[MAX_FLOWS]; 
double gaps[MAX_FLOWS], arrivaltime[MAX_FLOWS];

int numFlows, numSenders, numReceivers; //Cmdline arguments. 
double endtime;                         //Cmdline arguments.
char input_filename[255]; //Cmdline argument

//The workload intensity:
double lambda;//the poisson parameter. MEAN=1/lambda flows/sec. lambda=1/flows+per_sec 
double flows_per_sec; //How many flows to generate in the 10-sec window in order to achieve a certain network load?

int determine_lambda(double network_load) {
  //Change these parameters to adjust the network speed and network load.
  double bisection_bandwidth = 40000000000; // 40Gbps per sender
//  double network_load = 1; //80% network load.
  double traffic_rate = bisection_bandwidth * network_load/8; //The desired traffic rate, in bytes

  //Compute the average flow size:
  double avg_flow_size = 0;
  for (int i = 1; i < input_linecnt; i ++) {
    double temp = (input_percentage[i]-input_percentage[i-1])*(input_flowsize[i]+input_flowsize[i-1])/2; //Flowsize is in bytes
    avg_flow_size += temp; 
  }
  printf("Average flow size: %lf bytes\n", avg_flow_size);

  //TODO: Find out why the estimate is 10x off!!!
  flows_per_sec = traffic_rate/avg_flow_size;
  double interflow_gap = 1/flows_per_sec;
  lambda = flows_per_sec;
  printf("To achieve %lf load on a %lf bps network, we need %lf fps, and interflow gap (lambda) of %lf\n", network_load, bisection_bandwidth, flows_per_sec, interflow_gap);

  return 0;
}

int read_cdf_file() { 
  
  FILE *fp = fopen(input_filename, "r");
  if (!fp) {
    printf("Cannot find file. Exiting..");
    exit (-2);
  }
 
  int i = 0;
  while (fscanf(fp, "%lf%lf", &input_flowsize[i], &input_percentage[i]) != EOF) {
    if (i>=100) {
      printf("!?! More than 100\n");
      exit (-3);
    }
    i ++;
  }

  input_linecnt = i;
  return 0;
}



int generate_flow_arrival_time(double lambda, double endtime) {
  printf("generate_flow_arrival_time: lambda is %lf\n", lambda);

  // construct a trivial random generator engine from a time-based seed:
  unsigned seed = std::chrono::system_clock::now().time_since_epoch().count();
  std::default_random_engine generator (seed);

  //std::poisson_distribution<int> distribution (lambda);
  std::exponential_distribution<double> distribution (lambda);

  double starttime = 0;
  numFlows = 0;
//  for (int i = 0; i < numFlows ; i ++) {
  gaps[numFlows] = distribution(generator);
  starttime += gaps[numFlows]; 
  while (starttime <= endtime) {
    arrivaltime[numFlows++] = starttime;
    gaps[numFlows] = distribution(generator);
    starttime += gaps[numFlows]; 
  }

  return 0; 
}

int generate_workload(const char *output_filename) {

  //workload file:
//  char output_filename[255];
//  strcpy(output_filename, input_filename);
//  output_filename[strlen(output_filename)-3] = 'd';
//  output_filename[strlen(output_filename)-2] = 'a';
//  output_filename[strlen(output_filename)-1] = 't';
  FILE *fout = fopen(output_filename, "w");

  srand(time(NULL)); 

//  // Pick arrival time and flowsize, and then assign to random sender, random receiver
//  generate_flow_arrival_time(lambda, endtime);
//
//  for (int i = 0; i < numFlows; i ++) {
//
//    int theSender = rand() % numSenders;
//    int theReceiver = rand() % numReceivers;
//
//    //Pick a random percentage index in the CDF:
//    double x = (rand()%100) *1.00 / 100.00; // Random number between 0.00--1.00
//    printf("Coin toss result: %lf\n", x);
//    double theFlowsize;
//    for (int k = 0; k < input_linecnt-1; k ++) {
//      if ((x>=input_percentage[k]) && (x<=input_percentage[k+1])) {
//        theFlowsize = input_flowsize[k+1];
//        printf("percentage is %lf and flowsize is %lf\n", input_percentage[k], theFlowsize);
//        break;
//      }
//    }
//  
//    //log  
//    printf("From %d to %d with flowsize %lf, start time %lf\n", theSender, theReceiver, theFlowsize, arrivaltime[i]);
//  
//    //Save as file
//    fprintf(fout, "%lf %d %d %lf\n", arrivaltime[i], theSender, theReceiver, theFlowsize);
//   
//  }

//  for (int theReceiver = 0; theReceiver < numReceivers; theReceiver++) {
//    //Pick a random flow arrival time: 
//    generate_flow_arrival_time(lambda, endtime); 
//
////    //Fixed receiver, randomly pick ONE sender, and flow size. 
////    int theSender = rand() % numSenders;
//    for (int i = 0; i < numFlows; i ++) {
//      //Pick a random receiver and a random sender:
//      int theSender = rand() % numSenders;
////      int theReceiver = rand() % numReceivers;
//  
//      //Pick a random percentage index in the CDF:
//      double x = (rand()%100) *1.00 / 100.00; // Random number between 0.00--1.00
//      printf("Coin toss result: %lf\n", x);
//      double theFlowsize;
//      for (int k = 0; k < input_linecnt-1; k ++) {
//        if ((x>=input_percentage[k]) && (x<=input_percentage[k+1])) {
//          theFlowsize = input_flowsize[k+1];
//          printf("percentage is %lf and flowsize is %lf\n", input_percentage[k], theFlowsize);
//          break;
//        }
//      }
//   
//      //log  
//      printf("From %d to %d with flowsize %lf, start time %lf\n", theSender, theReceiver, theFlowsize, arrivaltime[i]);
//  
//      //Save as file
//      fprintf(fout, "%lf %d %d %lf\n", arrivaltime[i], theSender, theReceiver, theFlowsize);
//    }
//  }

  for (int theSender = 0; theSender < numSenders; theSender++) {
    //Pick a random flow arrival time: 
    generate_flow_arrival_time(lambda, endtime); 

//    //Fixed sender, randomly pick ONE receiver, and flow size. 
//    int theReceiver = rand() % numReceivers;
    for (int i = 0; i < numFlows; i ++) {
      //Pick a random sender and a random receiver:
//      int theSender = rand() % numSenders;
      int theReceiver = rand() % numReceivers;
  
      //Pick a random percentage index in the CDF:
      double x = (rand()%100) *1.00 / 100.00; // Random number between 0.00--1.00
      printf("Coin toss result: %lf\n", x);
      double theFlowsize;
      for (int k = 0; k < input_linecnt-1; k ++) {
        if ((x>=input_percentage[k]) && (x<=input_percentage[k+1])) {
          theFlowsize = input_flowsize[k+1];
          printf("percentage is %lf and flowsize is %lf\n", input_percentage[k], theFlowsize);
          break;
        }
      }
   
      //log  
      printf("From %d to %d with flowsize %lf, start time %lf\n", theSender, theReceiver, theFlowsize, arrivaltime[i]);
  
      //Save as file
      fprintf(fout, "%lf %d %d %lf\n", arrivaltime[i], theSender, theReceiver, theFlowsize);
    }
  }
  fclose(fout);

  return 0; 
}

int main (int argc, char *argv[]) {

  printf("Usage: ./generate InputCDFFileName endFlowLaunchTime NumOfSenders NumOfReceivers\n");
  if (argc != 5) {
    printf("Error: Wrong number of arguments. See usage above.\n");
    exit(-1);
  } else {
    strcpy(input_filename, argv[1]);
    int a = strcmp(input_filename, "FB_CDF.txt");
    int b = strcmp(input_filename, "VL2_CDF.txt");
    int c = strcmp(input_filename, "DCTCP_CDF.txt");
    int d = strcmp(input_filename, "CACHE_CDF.txt");
    assert(!a||!b||!c||!d);
    //numFlows = atoi(argv[2]);
    endtime = atof(argv[2]);
    numSenders = atoi(argv[3]);
    numReceivers = atoi(argv[4]);

//    if (numFlows > MAX_FLOWS) {
//      printf("Max number of flows: %d\n", MAX_FLOWS);
//      exit(-4);
//    } else if (numFlows < MIN_FLOWS) { 
//      printf("Min number of flows: %d If you generate too few flows, the the sampling is very inaccurate!\n", MIN_FLOWS);
//      exit(-4);
//    } else if (numSenders > MAX_NODES) {
    if (numSenders > MAX_NODES) {
      printf("Max number of senders: %d\n", MAX_NODES);
      exit(-4);
    } else if (numReceivers > MAX_NODES) {
      printf("Max number of receivers: %d\n", MAX_NODES);
    } else {
      //Things look good!
      printf("Generating flows until %lf sec, from %d senders to %d receivers; sampling from CDF: %s\n", endtime, numSenders, numReceivers, input_filename);
    } 
  }

  read_cdf_file();
  char filename[255];
  char intBuf[20];

//  determine_lambda(1);
//  generate_workload("tmp.txt");
  for (int j = 0; j < 4; j++) {
    for (int i = 0; i < 10; i++) {
      strcpy(filename, "DCTCP_Conga_40Gbps/DCTCP_CDF_PER_FLOW_UNTIL_");
      sprintf(intBuf, "%d", (i+1)*10);
      strcat(filename, intBuf);
      sprintf(intBuf, "_%.1f_%d_", endtime, numSenders);
      strcat(filename, intBuf);
      sprintf(intBuf, "%d_", numReceivers);
      strcat(filename, intBuf);
      sprintf(intBuf, "%d", j+1);
      strcat(filename, intBuf);
      strcat(filename, ".dat");
      determine_lambda((i+1)*0.1);
      generate_workload(filename);
    }
  }

  return 0;
}
