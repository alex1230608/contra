dir = "~/fattree_result_MU_FTO50_pkttags_scapy/";
load = "60";
dataset = "DCTCP_1";

kary = 6;
num_endHosts = (kary*kary)/4*kary;
start_endHost = kary*kary + kary*kary/4 + 1;

receiverTors = [ (kary/2)^2+1 : (kary/2)^2*2];

allPaths = [];
for j = 1:length(receiverTors)
    for port = 2:4
        filename = dir+"s"+num2str(receiverTors(j))+"-eth"+num2str(port)+".pktpaths.log"
        W = textscan(fopen(filename), "%s %s %s %s %s %s %s %s %s %s %s %s %s");
        path = zeros(length(W{1})-1, length(W));
        for hop = 1:length(W)
            path(:, hop) = str2double(W{hop}(2:length(W{hop})));
        end
        allPaths = [allPaths; path];
    end
end
