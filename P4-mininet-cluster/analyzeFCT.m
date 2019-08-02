% loads = ["10", "30", "50", "70", "90"];
% xlabs = [10:20:90];
% loads = ["90"];
% xlabs = [90];
% loads = ["70"];
% xlabs = [70];
% dirs = ["~/result/", "~/result_ecmp/", "~/result_hula/", ...
%     "~/result_37-28_down/", "~/result_hula_37-28_down/", ...
%     "~/result_hula_37-28_40-32_down/", "~/result_hula_37-28_40-32_43-36_down/", ...
%     "~/result_ecmp_37-28_40-32_43-36_down/", "~/result_ecmp_37-28_down/"];
sepDirs{1} = ["~/result_MU/", "~/result_ecmp/", "~/result/", "~/result_hula/"];
% sepDirs{2} = ["~/result_ecmp_37-28_down/", "~/result_37-28_down/", "~/result_hula_37-28_down/"];
% sepDirs{3} = ["~/result_hula_37-28_40-32_down/"];
% sepDirs{4} = ["~/result_ecmp_37-28_40-32_43-36_down/", "~/result_hula_37-28_40-32_43-36_down/"];
sepDirs{2} = ["~/result_MU_37-19_40-23_43-27_down/", "~/result_ecmp_37-19_40-23_43-27_down/", "~/result_37-19_40-23_43-27_down/", "~/result_hula_37-19_40-23_43-27_down/"];
sepDirs{3} = [sepDirs{1}, sepDirs{2}];
sepDirs{4} = ["~/result_hula/", "~/result_hula_pFreq128_FTO200/", ...
    "~/result_hula_pFreq64_FTO200/", "~/result_hula_pFreq32_FTO200/"];
sepDirs{5} = ["~/result_hula/", "~/result_hula_pFreq256_FTO100/", ...
    "~/result_hula_pFreq256_FTO50/", "~/result_hula_pFreq256_FTO25/", ...
    "~/result_hula_pFreq256_FTO10/", "~/result_hula_pFreq256_FTO5/"];
sepDirs{6} = ["~/BT_result_WSP/", "~/BT_result_MU/", "~/BT_result_SP/"];
sepDirs{7} = ["~/BT_testOnePair_result_WSP/", "~/BT_testOnePair_result_SP/"];
sepDirs{8} = ["~/BT_testOnePair_result_oneEH_WSP/", "~/BT_testOnePair_result_oneEH_SP/"];
sepDirs{9} = ["~/BT_result_twoEH_WSP/", "~/BT_result_twoEH_SP/", "~/BT_result_twoEH_MU/", ...
    "~/BT_result_twoEH_WSP_pFreq1024/", "~/BT_result_twoEH_WSP_pFreq512/", ...
    "~/BT_result_twoEH_WSP_pFreq128/", "~/BT_result_twoEH_WSP_pFreq64/", ...
    "~/BT_result_twoEH_WSP_pFreq32/", "~/BT_result_twoEH_WSP_pFreq16/"];
sepDirs{10} = ["~/BT_result_twoEH_WSP_FTO800/", "~/BT_result_twoEH_WSP_FTO400/", "~/BT_result_twoEH_WSP/", ...
    "~/BT_result_twoEH_WSP_FTO100/", "~/BT_result_twoEH_WSP_FTO50/", ...
    "~/BT_result_twoEH_WSP_FTO20/", "~/BT_result_twoEH_WSP_FTO10/"];
sepDirs{11} = ["~/BT_result_twoEH_WSP_FTO50/", "~/BT_result_twoEH_WSP_trace60/", ...
    "~/BT_result_twoEH_WSP_trace120/"];
sepDirs{12} = ["~/BT_testOnePair_70_result_oneEH_WSP_trace30/", ...
    "~/BT_testOnePair_70_result_oneEH_WSP_trace60/", ...
    "~/BT_testOnePair_70_result_oneEH_WSP_trace120/"];
sepDirs{13} = ["~/BT_result_oneEH_WSP_trace30/", ...
    "~/BT_result_oneEH_WSP_trace60/", ...
    "~/BT_result_oneEH_WSP_trace120/"];
sepDirs{14} = ["~/BT_result_oneEH_goodSel_WSP_trace30/", ...
    "~/BT_result_oneEH_goodSel_WSP_trace60/", ...
    "~/BT_result_oneEH_goodSel_WSP_trace120/", ...
    "~/BT_result_oneEH_goodSel_SP_trace30/", ...
    "~/BT_result_oneEH_goodSel_SP_trace60/", ...
    "~/BT_result_oneEH_goodSel_SP_trace120/"];
sepDirs{15} = ["~/BT_result_oneEH_4_4_WSP_trace30/", ...
    "~/BT_result_oneEH_4_4_WSP_trace60/", ...
    "~/BT_result_oneEH_4_4_WSP_trace120/"];
sepDirs{16} = ["~/BT_result_4senders_MU/", "~/BT_result_4senders_WSP/", ...
    "~/BT_result_4senders_SP/", "~/BT_result_4senders_SPAIN/"];
sepDirs{17} = ["~/loadBal_result_fattree45_contra/", "~/loadBal_result_fattree45_hula/", "~/loadBal_result_fattree45_ecmp/"];
sepDirs{18} = ["~/Abilene_result_SPAIN/", "~/Abilene_result_WSP_FTO50/", ...
    "~/Abilene_result_SP_FTO50/", "~/Abilene_result_MU_FTO50/"];
sepDirs{19} = ["~/Abilene_result_trace300_SPAIN/", "~/Abilene_result_trace300_WSP_FTO50/", ...
    "~/Abilene_result_trace300_SP_FTO50/", "~/Abilene_result_trace300_MU_FTO50/"];
sepDirs{20} = ["~/Abilene_result_perfectPlacement_SPAIN/", ...
    "~/Abilene_result_perfectPlacement_WSP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_SP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_MU_FTO50/"];
sepDirs{21} = ["~/Abilene_result_perfectPlacement_queue2000_SPAIN/", ...
    "~/Abilene_result_perfectPlacement_queue2000_WSP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_queue2000_SP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_queue2000_MU_FTO50/"];
sepDirs{22} = ["~/Abilene_result_perfectPlacement_newSender_SPAIN/", ...
    "~/Abilene_result_perfectPlacement_newSender_WSP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_newSender_SP_FTO50/", ...
    "~/Abilene_result_perfectPlacement_newSender_MU_FTO50/"];
sepDirs{23} = ["~/fct_abilene/Abilene_result_Full_SPAIN/", ...
    "~/fct_abilene/Abilene_result_Full_WSP_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_SP_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_MU_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_SPAIN_SP/"];
sepDirs{24} = ["~/fattree_result_WSP_FTO50/", ...
    "~/fattree_result_MU_FTO50_secondRun/", ...
    "~/fattree_result_ecmp/", ...
    "~/fattree_result_hula_FTO50/"];
sepDirs{25} = ["~/fct_abilene/Abilene_result_Full_CACHE_SPAIN/", ...
    "~/fct_abilene/Abilene_result_Full_CACHE_WSP_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_CACHE_SP_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_CACHE_MU_FTO50/", ...
    "~/fct_abilene/Abilene_result_Full_CACHE_SPAIN_SP/"];
sepDirs{26} = sepDirs{24};
sepDirs{27} = ["~/fattree_result_fail_WSP_FTO50/", ...
    "~/fattree_result_fail_MU_FTO50/", ...
    "~/fattree_result_fail_ecmp/", ...
    "~/fattree_result_fail_hula_FTO50/"];
sepDirs{28} = sepDirs{27};
sepDirs{29} = ["~/fattree_result_MU_FTO50_pkttags_test/", ...
    "~/fattree_result_MU_FTO50/"];
% schemes = ["contra", "ecmp", "hula", ...
%     "contra-1link-down", "hula-1link-down", ...
%     "hula-2links-down", "hula-3links-down", ...
%     "ecmp-3links-down", "ecmp-1link-down"];
sepSchemes{1} = ["contra-MU", "ecmp", "contra-WSP", "hula"];
% sepSchemes{2} = ["ecmp-1link-down", "contra-1link-down", "hula-1link-down"];
% sepSchemes{3} = ["hula-2links-down"];
% sepSchemes{4} = ["ecmp-3links-down", "hula-3links-down"];
sepSchemes{2} = ["contra-MU-3links-down", "ecmp-3links-down", "contra-WSP-3links-down", "hula-3links-down"];
sepSchemes{3} = [sepSchemes{1}, sepSchemes{2}];
sepSchemes{4} = ["hula-pFreq256-FTO200", "hula-pFreq128-FTO200", ...
    "hula-pFreq64-FTO200", "hula-pFreq32-FTO200"];
sepSchemes{5} = ["hula-pFreq256-FTO200", "hula-pFreq256-FTO100", ...
    "hula-pFreq256-FTO50", "hula-pFreq256-FTO25", ...
    "hula-pFreq256-FTO10", "hula-pFreq256-FTO5"];
sepSchemes{6} = ["contra-WSP", "contra-MU", "contra-SP"];
sepSchemes{7} = ["contra-WSP-twoSender", "contra-SP-twoSender"];
sepSchemes{8} = ["contra-WSP-oneSender", "contra-SP-oneSender"];
sepSchemes{9} = ["contra-WSP-pFreq256", "contra-SP", "contra-MU", ...
    "contra-WSP-pFreq1024", "contra-WSP-pFreq512", ...
    "contra-WSP-pFreq128", "contra-WSP-pFreq64", ...
    "contra-WSP-pFreq32", "contra-WSP-pFreq16"];
sepSchemes{10} = ["contra-WSP-FTO800", "contra-WSP-FTO400", "contra-WSP-FTO200", ...
    "contra-WSP-FTO100", "contra-WSP-FTO50", ...
    "contra-WSP-FTO20", "contra-WSP-FTO10"];
sepSchemes{11} = ["contra-WSP-trace30", "contra-WSP-trace60", ...
    "contra-WSP-trace120"];
sepSchemes{12} = ["contra-WSP-trace30", "contra-WSP-trace60", ...
    "contra-WSP-trace120"];
sepSchemes{13} = ["contra-WSP-trace30", "contra-WSP-trace60", ...
    "contra-WSP-trace120"];
sepSchemes{14} = ["contra-WSP-trace30", "contra-WSP-trace60", ...
    "contra-WSP-trace120", "contra-SP-trace30", ...
    "contra-SP-trace60", "contra-SP-trace120"];
sepSchemes{15} = [ "contra-WSP-trace30", "contra-WSP-trace60", ...
    "contra-WSP-trace120"];
sepSchemes{16} = ["contra-MU", "contra-WSP", ...
    "contra-SP", "SPAIN"];
sepSchemes{17} = ["contra-WSP", ...
    "hula", "ecmp"];
sepSchemes{18} = ["SPAIN", "contra-WSP", "contra-SP", "contra-MU"];
sepSchemes{19} = ["SPAIN", "contra-WSP", "contra-SP", "contra-MU"];
sepSchemes{20} = ["SPAIN", "contra-WSP", "contra-SP", "contra-MU"];
sepSchemes{21} = ["SPAIN", "contra-WSP", "contra-SP", "contra-MU"];
sepSchemes{22} = ["SPAIN", "contra-WSP", "contra-SP", "contra-MU"];
sepSchemes{23} = ["spain", "wsp", "dynamic-sp", "mu", "static-sp"];
sepSchemes{24} = ["wsp", "mu", "ecmp", "hula"];
sepSchemes{25} = ["spain", "wsp", "dynamic-sp", "mu", "static-sp"];
sepSchemes{26} = sepSchemes{24};
sepSchemes{27} = ["wsp", "mu", "ecmp", "hula"];
sepSchemes{28} = sepSchemes{27};
sepSchemes{29} = ["mu-pkttags", "mu"];
sepLoads = cell(length(sepSchemes), 1);
sepLoads{4} = ["10", "30", "50", "70", "90"];
sepLoads{5} = ["10", "30", "50", "70", "90"];
sepLoads{9} = ["90"];
sepLoads{10} = ["90"];
sepLoads{13} = ["90"];  % actually it's 70% workload
sepLoads{14} = ["70"];
sepLoads{15} = ["70"];
sepLoads{16} = ["10", "30", "50", "70", "90"];
sepLoads{17} = ["10", "30", "50", "70", "90"];
sepLoads{18} = ["10", "30", "50", "70", "90"];
sepLoads{19} = ["10", "30", "50", "70", "90"];
sepLoads{20} = ["60", "70", "80"];
sepLoads{21} = ["60", "70", "80"];
sepLoads{22} = ["60", "70", "80"];
sepLoads{23} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{24} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{25} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{26} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{27} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{28} = ["10", "20", "30", "40", "50", "60", "70", "80", "90"];
sepLoads{29} = ["90"];
sepXLabs = cell(length(sepSchemes), 1);
sepXLabs{4} = [10:20:90];
sepXLabs{5} = [10:20:90];
sepXLabs{9} = [90];
sepXLabs{10} = [90];
sepXLabs{13} = [90];
sepXLabs{14} = [70];
sepXLabs{15} = [70];
sepXLabs{16} = [10:20:90];
sepXLabs{17} = [10:20:90];
sepXLabs{18} = [10:20:90];
sepXLabs{19} = [10:20:90];
sepXLabs{20} = [60:10:80];
sepXLabs{21} = [60:10:80];
sepXLabs{22} = [60:10:80];
sepXLabs{23} = [10:10:90];
sepXLabs{24} = [10:10:90];
sepXLabs{25} = [10:10:90];
sepXLabs{26} = [10:10:90];
sepXLabs{27} = [10:10:90];
sepXLabs{28} = [10:10:90];
sepXLabs{29} = [90];
BT_senders = cell(length(sepSchemes),1);
BT_senders{6} = [34,17,2,31,1,16,15,21,7,35,29,6,3,23];
BT_senders{7} = [1,2];
BT_senders{8} = [1];
BT_senders{9} = [41,63,15,23,59,29,11,61,25,5,3,65,53,33,42,64,16,24,60,30,12,62,26,6,4,66,54,34];
BT_senders{10} = BT_senders{9};
BT_senders{11} = BT_senders{9};
BT_senders{12} = [1];
BT_senders{13} = BT_senders{9}(1:14);
BT_senders{14} = [49,65,1,27,29,3,53,35,55,21,51,37,71,7];
BT_senders{15} = [41,63,15,23];
BT_senders{16} = BT_senders{15};
BT_senders{18} = [21,15,1,17];
BT_senders{19} = BT_senders{18};
BT_senders{20} = BT_senders{18};
BT_senders{21} = BT_senders{18};
BT_senders{22} = [7,17,15,1];
BT_senders{23} = [7,17,15,1];
BT_senders{25} = [7,17,15,1];
offsets = [0, 0, 0, 0, 0, ...
    36, 36, 36, 36, 36, ...
    36, 36, 36, 36, 36, ...
    36, 0, 11, 11, 11, ...
    11, 11, 11, 0, 11, ...
    0, 0, 0, 0];
sepDatasets = cell(length(sepSchemes),1);
[sepDatasets{1:22}] = deal(["1"]);
sepDatasets{23} = ["1", "2", "3", "4"];
sepDatasets{24} = ["DCTCP_1", "DCTCP_2", "DCTCP_3", "DCTCP_4"];
sepDatasets{25} = ["1", "2", "3", "4"];
sepDatasets{26} = ["CACHE_1", "CACHE_2", "CACHE_3", "CACHE_4"];
sepDatasets{27} = ["DCTCP_1", "DCTCP_2", "DCTCP_3", "DCTCP_4"];
sepDatasets{28} = ["CACHE_1", "CACHE_2", "CACHE_3", "CACHE_4"];
sepDatasets{29} = ["DCTCP_1"];
sepOutDataName = cell(length(sepSchemes),1);
sepOutDataName{23} = "ws";
sepOutDataName{24} = "ws";
sepOutDataName{25} = "cache";
sepOutDataName{26} = "cache";
sepOutDataName{27} = "ws";
sepOutDataName{28} = "cache";
sepOutDataName{29} = "ws";
sepOutTopoName = cell(length(sepSchemes),1);
sepOutTopoName{23} = "abilene";
sepOutTopoName{24} = "fattree";
sepOutTopoName{25} = "abilene";
sepOutTopoName{26} = "fattree";
sepOutTopoName{27} = "fattree";
sepOutTopoName{28} = "fattree";
sepOutTopoName{29} = "fattree";
sepOutAsymDataName = cell(length(sepSchemes),1);
sepOutAsymDataName{23} = "";
sepOutAsymDataName{24} = "";
sepOutAsymDataName{25} = "";
sepOutAsymDataName{26} = "";
sepOutAsymDataName{27} = "asym-";
sepOutAsymDataName{28} = "asym-";
sepOutAsymDataName{29} = "";
for i = 1:length(BT_senders)
    BT_senders{i} = BT_senders{i} + offsets(i)*ones(1, length(BT_senders{i}));
end
% sepTitles = ["Symmetric", "one link down", "two links down", "three links down"];
sepTitles = ["Symmetric", "three links down", "All in one", ...
    "Check probe period", "Check flowlet timeout", ...
    "BT North America", "Check BT one Pair twoEH", "Check BT one Pair oneEH", ...
    "BT North America with two end host per sw (try pFreq)", ...
    "BT North America with two end host per sw (try FTO)", ...
    "BT North America with two end host per sw (try traceLen)", ...
    "test one pair one EH (try traceLen)", ...
    "BT North America with one end host per sw (try traceLen)", ...
    "BT North America with one end host per sw (good selection) (try traceLen)", ...
    "BT North America with one end host per sw (4 senders) (try traceLen)", ...
    "BT North America", ...
    "Symmetric", ...
    "Abilene", ...
    "Abilene (traceLen300)", ...
    "Abilene, queue1000 (perfectPlacement)", ...
    "Abilene, queue2000 (perfectPlacement)", ...
    "Abilene, newSender (perfectPlacement)", ...
    "Abilene, web search", ...
    "Fattree, web search, symmetric", ...
    "Abilene, CACHE", ...
    "Fattree, CACHE, symmetric", ...
    "Fattree, web search, asymmetric", ...
    "Fattree, CACHE, asymmetric", ...
    "Fattree, web search, symmetric, with pkttags"];

kary = 6;
num_endHosts = (kary*kary)/4*kary;
start_endHost = kary*kary + kary*kary/4 + 1;

for s = 23:23%length(sepDirs)
    dirs = sepDirs{s};
    schemes = sepSchemes{s};
    loads = sepLoads{s};
    xlabs = sepXLabs{s};
    datasets = sepDatasets{s};
    data = cell(length(dirs), length(loads), length(datasets));
    FCTs = cell(length(dirs), length(loads), length(datasets));
    maxFCTs = zeros(length(dirs), length(loads), length(datasets));
    meanFCTs = zeros(length(dirs), length(loads), length(datasets));
    smallFlowMeanFCTs = zeros(length(dirs), length(loads), length(datasets));
    largeFlowMeanFCTs = zeros(length(dirs), length(loads), length(datasets));
    startDoubles = cell(length(dirs), length(loads), length(datasets));
%     mycdfs = cell(1,length(loads));
    for d = 1:length(dirs)
        for dataSetId = 1:length(datasets)
            for i = 1:length(loads)
    %             if d == 1
    %                 mycdfs{i} = figure;
    %             end
                FCT = [];
                flowSize = [];
                startTime = [];
                endTime = [];
                startDoubles{d, i, dataSetId} = [];
                if offsets(s) == 0
                    senders = [start_endHost : start_endHost + num_endHosts/2 - 1];
                else
                    senders = BT_senders{s};
                end
                for j = 1:length(senders)
                    if s <= 22
                        filename = dirs(d)+"h"+num2str(senders(j))+"_"+loads(i)+".log";
                    else
                        filename = dirs(d)+"h"+num2str(senders(j))+"_"+datasets(dataSetId)+"_"+loads(i)+".log"
                    end
                    W = textscan(fopen(filename), "%s %s %s %s %s %s %s %s %s %s");
        %             [~,~,~, H, MN, S] = datevec(cell2mat(W{5}));
        %             FCT = [FCT; H.*3600+MN.*60+S];
                    FCT = [FCT; str2double(W{9})];
                    flowSize = [flowSize; str2double(W{4})];
                    startTime = [startTime; W{6}];
                    [~,~,~, H, MN, S] = datevec(W{6});
                    startDouble = [H.*3600+MN.*60+S];
                    startDouble = startDouble-min(startDouble);
                    startDoubles{d, i, dataSetId} = [startDoubles{d, i, dataSetId}; startDouble];
                    endTime = [endTime; W{8}];
                end
                data{d, i, dataSetId} = [startTime, endTime];
                FCTs{d, i, dataSetId} = FCT;
                maxFCTs(d, i, dataSetId) = max(FCT);
                meanFCTs(d, i, dataSetId) = mean(FCT);
                smallFlowMeanFCTs(d, i, dataSetId) = mean(FCT(flowSize<=100000));
                if sepOutDataName{s} == "cache"
                    largeFlowMeanFCTs(d, i, dataSetId) = mean(FCT(flowSize>=1000000));
                else
                    largeFlowMeanFCTs(d, i, dataSetId) = mean(FCT(flowSize>=10000000));
                end

    %             figure(mycdfs{i});
    %             hold on;
    %             cdfplot(FCT);
    %             if (d == length(dirs))
    %                 xlabel('FCT (sec)');
    %                 ylabel('CDF');
    %                 ylim([0.5 1]);
    %                 legend(schemes);
    %                 title('load '+loads(i)+'%');
    %             end
            end
        end
    end
    
%     figure;
%     hold on;
%     offset = [0, 30, 30+60, 30+60+120, 30+60+120+30, 30+60+120+30+60];
%     for d = length(dirs):-1:1
%         plot(startDoubles{d, 1}+offset(d), FCTs{d, 1}, '.');
%         xlabel('starting time');
%         ylabel('FCT');
%         legend(fliplr(schemes));
%         title(sepTitles(s));
%     end
    
    data
    maxFCTs
    meanFCTs
    
    for d = 1:length(dirs)
        figure('pos',[10 10 400 300]);
        plot(xlabs, reshape(meanFCTs(d, :, :), [length(loads), length(datasets)])', '-o');
        legend(datasets, 'Location','NorthWest');
        title('all flows, '+sepTitles(s)+', '+schemes(d));
        xlabel('workload (%)');
        ylabel('FCT (sec)');
        ylim([0, max(max(reshape(meanFCTs(d, :, :), [length(loads), length(datasets)])))]);
        saveas(gcf,'all_flows_'+sepTitles(s)+'_'+schemes(d)+'.jpg');
    end
    

    figure('pos',[10 10 400 300]);
    plot(xlabs, mean(meanFCTs, 3)', '-o');
    legend(schemes, 'Location','NorthWest');
    title('all flows, '+sepTitles(s));
    xlabel('workload (%)');
    ylabel('FCT (sec)');
    ylim([0, max(max(mean(meanFCTs, 3)))]);
    saveas(gcf,'all_flows_'+sepTitles(s)+'.jpg');

    figure('pos',[10 10 400 300]);
    plot(xlabs, mean(smallFlowMeanFCTs, 3)', '-o');
    legend(schemes, 'Location','NorthWest');
    title('small flows (<=100KB), '+sepTitles(s));
    xlabel('workload (%)');
    ylabel('FCT (sec)');
    ylim([0, max(max(mean(smallFlowMeanFCTs, 3)))]);
    saveas(gcf,'small_flows_'+sepTitles(s)+'.jpg');

    figure('pos',[10 10 400 300]);
    plot(xlabs, mean(largeFlowMeanFCTs, 3)', '-o');
    legend(schemes, 'Location','NorthWest');
    title('large flows (>=10MB), '+sepTitles(s));
    xlabel('workload (%)');
    ylabel('FCT (sec)');
    ylim([0, max(max(mean(largeFlowMeanFCTs, 3)))]);
    saveas(gcf,'large_flows_'+sepTitles(s)+'.jpg');
    
    X = mean(meanFCTs, 3)';
    Y = mean(smallFlowMeanFCTs, 3)';
    Z = mean(largeFlowMeanFCTs, 3)';
    
    for d = 1:length(dirs)
        fileID1 = fopen("testbed-fct-"+sepOutDataName{s}+'-'+sepOutTopoName{s}+'-'+sepOutAsymDataName{s}+sepSchemes{s}(d)+'.data','w');
        fileID2 = fopen("testbed-fct-"+sepOutDataName{s}+'-'+sepOutTopoName{s}+'-'+sepOutAsymDataName{s}+'small-'+sepSchemes{s}(d)+'.data','w');
        fileID3 = fopen("testbed-fct-"+sepOutDataName{s}+'-'+sepOutTopoName{s}+'-'+sepOutAsymDataName{s}+'large-'+sepSchemes{s}(d)+'.data','w');
        fprintf(fileID1, '#Index          Percentage          FlowCompletionTime\n');
        fprintf(fileID2, '#Index          Percentage          FlowCompletionTime\n');
        fprintf(fileID3, '#Index          Percentage          FlowCompletionTime\n');
        for l = 1:length(loads)
            fprintf(fileID1, '%d\t%d\t%f\n', l, 10*l, X(l, d));
            fprintf(fileID2, '%d\t%d\t%f\n', l, 10*l, Y(l, d));
            fprintf(fileID3, '%d\t%d\t%f\n', l, 10*l, Z(l, d));
        end
    end

end

% % For RMSE with simulation results, please get B from simulation result
% % first
% A = mean(meanFCTs, 3)';
% RMSE_sim_and_testbed = sqrt(mean((A-B).^2));
% 
% RMSE_testbed = zeros(1, size(meanFCTs, 1));
% for d = 1:size(meanFCTs, 1)
%     T = reshape(meanFCTs(d, :, :), [length(loads), length(datasets)]);
%     RMSE_testbed(d) = mean(sqrt(mean((T - A(:, d) * [1 1 1 1]).^2)));
% end
% 
% % For Correlation Coefficient
% CC_sim_and_testbed = zeros(1, size(meanFCTs, 1));
% CC_testbed = zeros(1, size(meanFCTs, 1));
% for d = 1:size(meanFCTs, 1)
%     temp = corrcoef(A(:, d), B(:, d));
%     CC_sim_and_testbed(d) = temp(1,2);
%     T = reshape(meanFCTs(d, :, :), [length(loads), length(datasets)]);
%     allTemp = zeros(1, size(T,2));
%     for tests = 1:size(T,2)
%         temp = corrcoef(A(:, d), T(:, tests));
%         allTemp(tests) = temp(1,2);
%     end
%     CC_testbed(d) = mean(allTemp);
% end