import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import pylab as pl
import collections
from matplotlib import collections  as mc

def get_cdf_list(lst):
    lst = sorted(lst)
    cdf_list = []
    size = 0
    len_list = len(lst)
    for value in lst:
        size += 1
        cdf = float(size)/len_list
        cdf_entry = (value,cdf)
        cdf_list.append(cdf_entry)
    return cdf_list

def get_cdf_tuple(tup):
    tup = sorted(tup,key=lambda x: x[0])
    cdf_list = []
    size = 0
    len_list = len(tup)
    for value in tup:
        size += 1
        cdf = float(size)/len_list
        cdf_entry = (value[0],cdf,value[1])
        cdf_list.append(cdf_entry)
    return cdf_list


def drawhist(filename,title,d,xlabel,ylabel):
    pl.title(title)
    pl.xlabel(xlabel)
    pl.ylabel(ylabel)
    X=np.arange(0,len(d))
    pl.bar(X, d.values(), align='center', width=0.5)
    pl.xticks(X, d.keys())
    ymax = max(d.values()) + 2
    pl.ylim(0, ymax)
    pl.tight_layout()
    pl.savefig(filename)
    pl.close()

def barGraph(filename, title, data, xlabel, ylabel, xtics, xlim=None, ylim=None, pos='upper right',legends=[], errorbars=False):
    leg_list=[]
    if ylim:
        plt.ylim(ylim)
    if xlim:
        plt.xlim(xlim)
    leg_list = legends
    xy_list = zip(*data)
    x=list(xy_list[0])
    y=list(xy_list[1])
    z=list(xy_list[2])
    a=list(xy_list[3])
    #k=list(xy_list[4])
    ind = np.arange(5)
    width=0.5
    print x
    print y
    error_kw=dict(ecolor='black', lw=2, capsize=5)
    if errorbars:
        xy_err_list = zip(*errorbars)
        x_err=list(xy_err_list[0])
        y_err=list(xy_err_list[1])
        z_err=list(xy_err_list[2])
        a_err=list(xy_err_list[3])
        #k_err=list(xy_err_list[4])
        p1 = plt.bar(ind, x, width, color='b',yerr=x_err,error_kw=error_kw)
        p2 = plt.bar(ind, y, width, bottom=x, color='y',yerr=y_err)
        p3 = plt.bar(ind, z, width, bottom=[x[i]+y[i] for i in range(len(x))], color='c',yerr=z_err,error_kw=error_kw)
        p4 = plt.bar(ind, a, width, bottom=[x[i]+y[i]+z[i] for i in range(len(x))], color='r',yerr=a_err,error_kw=error_kw)
        #p5 = plt.bar(ind, k, width, bottom=[x[i]+y[i]+z[i]+a[i] for i in range(len(x))], color='g',yerr=k_err,error_kw=error_kw)
    else:
        p1 = plt.bar(ind, x, width, color='b')
        p2 = plt.bar(ind, y, width, bottom=x, color='y')
        p3 = plt.bar(ind, z, width, bottom=[x[i]+y[i] for i in range(len(x))], color='c')
        p4 = plt.bar(ind, a, width, bottom=[x[i]+y[i]+z[i] for i in range(len(x))], color='r')
        #p5 = plt.bar(ind, k, width, bottom=[x[i]+y[i]+z[i]+a[i] for i in range(len(x))], color='g')
    pl.xlabel(xlabel)
    pl.ylabel(ylabel)
    pl.title(title)
    pl.xticks(ind+0.15, xtics)
    pl.legend((p1[0], p2[0], p3[0], p4[0]), leg_list, loc=pos)
    pl.savefig(filename)
    pl.close()

def drawdisconnectedlines(filename,title,lines,legend,xlabel,ylabel,xtics=[],xlim=None,ylim=None,pos="lower right",legends=[]):
    #lines = [[(0, 1), (1, 1)], [(2, 3), (3, 3)], [(1, 2), (1, 3)]]
    leg_list=[]
    if ylim:
        plt.ylim(ylim)

    #c = np.array([(1, 0, 0, 1), (0, 1, 0, 1), (0, 0, 1, 1)])
    lc = mc.LineCollection(lines, linewidths=2)
    fig, ax = plt.subplots()
    ax.add_collection(lc)
    ax.autoscale()
    ax.margins(0.1)
    if xlim:
        ax.set_xlim([0,xlim])
    plt.legend([legend],loc=pos)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.title(title)
    plt.savefig(filename,bbox_inches='tight')
    plt.close()


def drawsubplots(filename,title,d,xlabel,ylabel,xtics=[],xlim=None,ylim=None,pos='upper right',legends=[]):
    leg_list=[]
    display=1
    display_lst=[]
    colors=['r','g','b']
    if ylim:
        plt.ylim(ylim)
    if xlim:
        plt.xlim(xlim)
    #plt.xlim([0,100])
    if len(legends) > 0:
        leg_list = legends
    else:
        leg_list = d.keys()
    for leg in leg_list:
        plot_list = d[leg]
        x=[]
        y=[]
        xy_list = zip(*plot_list)
        x=list(xy_list[0])
        y=list(xy_list[1])
        plt.subplot(3,1,display)
        display_lst.append(display)
        display +=1
        plt.plot(x,y, linestyle='-',marker='o',label=leg,color=colors[display-2])
        if xlim:
            plt.xlim(xlim)
        if ylim:
            plt.ylim(ylim)

    #for disp in display_lst[1:]:
    #    plt.subplot(3,1,disp)
    #    ax = plt.gca()
    #    ax.set_xticklabels([])

    for idx in range(len(display_lst)):
        plt.subplot(3,1,display_lst[idx])
        ax = plt.gca()
        ax.set_ylabel(ylabel)
        ax.legend(loc=pos)


    #plt.legend(leg_list,loc=pos)
    plt.subplot(3,1,display_lst[-1])
    ax = plt.gca()
    ax.set_xlabel(xlabel)
    #plt.ylabel(ylabel)
    #plt.title(title)
    plt.savefig(filename,bbox_inches='tight')
    plt.close()



def drawlines(filename,title,d,xlabel,ylabel,xtics=[],xlim=None,ylim=None,pos="upper right",legends=[],errorbars=False, disconnectedlines=False, disconn_data=None):
    leg_list=[]
    if ylim:
        plt.ylim(ylim)
    if xlim:
        plt.xlim(xlim)
    #plt.xlim([0,100])
    if len(legends) > 0:
        leg_list = legends
    else:
        leg_list = d.keys()
    for leg in d.keys():
        plot_list = d[leg]
        x=[]
        y=[]
        #leg_list.append(str(int(leg)*4)+"K")
        #xy=collections.OrderedDict(sorted(xy.items()))
        xy_list = zip(*plot_list)
        x=list(xy_list[0])
        y=list(xy_list[1])
        #print x,y
        if errorbars:
            yerr=list(xy_list[2])
            for i in range(len(x)):
                plt.errorbar(x[i],y[i],yerr=yerr[i])
        #if len(xy_list) > 2 and len(xtics)==0:
        #    for i in range(0,len(x)):
        #        xtics.append(str(xy_list[0][i])+"_"+str(xy_list[2][i])+"_"+str(xy_list[3][i]))
        plt.plot(x,y, linestyle='-',marker='o',)
    if disconnectedlines:
        # disconn_data = [[(0, 1), (1, 1)], [(2, 3), (3, 3)], [(1, 2), (1, 3)]]
        #lc = mc.LineCollection(disconn_data, linewidths=2)
        #fig, ax = plt.subplots()
        #ax.add_collection(lc)
        #ax.autoscale()
        plt.plot(*disconn_data,points='o',linestyle='-')

    #if len(xtics) > 0:
        #plt.xticks(x,xtics,fontsize=10,ha="right",rotation=90)

    #plt.xticks(x,xtics)
    #print xtics
    plt.legend(leg_list,loc=pos)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.title(title)
    plt.savefig(filename,bbox_inches='tight')
    plt.close()


def drawmultihist1(filename,title,d,xlabel,ylabel):
    ind = np.arange(4)  # the x locations for the groups
    width = 0.15       # the width of the bars

    fig = plt.figure()
    ax = fig.add_subplot(111)
    print d["8"].values()
    print d["16"].values()
    rects1 = ax.bar(ind, sorted(d["1"].values()), width, align="center",color='r')
    rects2 = ax.bar(ind+(width), sorted(d["2"].values()), width, align="center", color='y')
    rects3 = ax.bar(ind+(2*width), sorted(d["4"].values()), width, align="center",color='b')
    rects4 = ax.bar(ind+(3*width), sorted(d["8"].values()), width, align="center", color='g')
    rects5 = ax.bar(ind+(4*width), sorted(d["16"].values()), width, align="center", color='orange')



    # add some
    ax.set_xlabel(xlabel)
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    ax.set_xticks(ind+width+0.1)
    ax.set_xticklabels( (str(10*4)+"K",str(20*4)+"K",str(30*4)+"K",str(60*4)+"K") )

    ax.legend( (rects1[0], rects2[0],rects3[0],rects4[0],rects5[0]), ('1', '2','4','8','16'),loc="upper left" )

    plt.savefig(filename)

def drawmultihist2(filename,title,d,xlabel,ylabel):
    ind = np.arange(8)  # the x locations for the groups
    width = 0.15       # the width of the bars

    fig = plt.figure()
    ax = fig.add_subplot(111)
    print d["8"].values()
    print d["16"].values()
    rects1 = ax.bar(ind, sorted(d["1"].values()), width, align="center",color='r')
    rects2 = ax.bar(ind+(width), sorted(d["2"].values()), width, align="center", color='y')
    rects3 = ax.bar(ind+(2*width), sorted(d["4"].values()), width, align="center",color='b')
    rects4 = ax.bar(ind+(3*width), sorted(d["8"].values()), width, align="center", color='g')
    rects5 = ax.bar(ind+(4*width), sorted(d["16"].values()), width, align="center", color='orange')



    # add some
    ax.set_xlabel(xlabel)
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    ax.set_xticks(ind+width+0.1)
    ax.set_xticklabels( (str(5)+"K",str(10)+"K",str(30)+"K",str(50)+"K",str(100)+"K",str(250)+"K",str(500)+"K",str(1000)+"K") )

    ax.legend( (rects1[0], rects2[0],rects3[0],rects4[0],rects5[0]), ('1', '2','4','8','16'),loc="upper left" )

    plt.savefig(filename)


def drawmultihist3(filename,title,d,xlabel,ylabel):
    ind = np.arange(1)  # the x locations for the groups
    width = 0.15       # the width of the bars

    fig = plt.figure()
    ax = fig.add_subplot(111)
    #print d["8"].values()
    #print d["16"].values()
    rects1 = ax.bar(ind, sorted(d["direct"].values()), width, align="center",color='r')
    rects2 = ax.bar(ind+(width), sorted(d["dynamic"].values()), width, align="center", color='y')
    #rects3 = ax.bar(ind+(2*width), sorted(d["4"].values()), width, align="center",color='b')
    #rects4 = ax.bar(ind+(3*width), sorted(d["8"].values()), width, align="center", color='g')
    #rects5 = ax.bar(ind+(4*width), sorted(d["16"].values()), width, align="center", color='orange')



    # add some
    ax.set_xlabel(xlabel)
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    ax.set_xticks(ind+width+0.1)
    #ax.set_xticklabels( (str(10*4)+"K",str(20*4)+"K",str(30*4)+"K",str(60*4)+"K") )
    ax.set_xticklabels( (str(60*4)+"K") )


    ax.legend( (rects1[0], rects2[0]), ('direct', 'QPA'),loc="upper left" )

    plt.savefig(filename)

def drawmultihist(filename,title,d,xlabel,ylabel):
    ind = np.arange(4)  # the x locations for the groups
    width = 0.35       # the width of the bars

    fig = plt.figure()
    ax = fig.add_subplot(111)
    rects1 = ax.bar(ind, d["imb_spray"].values(), width, align="center",color='r')


    rects2 = ax.bar(ind+width, d["bal_spray"].values(), width, align="center", color='y')

    # add some
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    ax.set_xticks(ind+width-0.15)
    ax.set_xticklabels( ("path-1","path-2","path-3","path-4") )

    ax.legend( (rects1[0], rects2[0]), ('Imbalance', 'Balance') )

    plt.savefig(filename)

def drawCDF(filename,title,plotdict,xlabel,ylabel,ylim=None,logscale=False):
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    #plt.xticks(np.arange(0,30,2))
    #plt.yticks(np.arange(0,450,20))
    #plt.ylim([-1,1000])
    legend_list = ["link 1","link 2"]

    plt.title(title)
    if ylim:
        plt.ylim([0,ylim])
    if logscale:
        plt.xscale('log')
    markers = ["s","o","*","d","D","x"]
    mrk_indx = 0
    for label,plotlist in plotdict.items():
        sorted_data=sorted(plotlist)

        plt.plot(sorted_data,np.linspace(0,1,len(sorted_data)),linewidth=2)
        #legend_list.append(label)
        mrk_indx += 1
    plt.legend(legend_list,loc = "upper left")
    #plt.show()
    plt.savefig(filename)
    plt.close()
        #legend_list.append(label)
    #plt.show()

'''def drawgraph(filename,plotdict,xlabel,ylabel):
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    #plt.xticks(np.arange(0,30,2))
    #plt.yticks(np.arange(0,450,20))
    #plt.ylim([-1,1000])
    legend_list = []
    for label,plotlist in plotdict.items():
        zip(*plotlist)
        plt.plot(*zip(*plotlist),marker='o')
        legend_list.append(label)
    plt.legend(legend_list,loc = "center right")
    #plt.show()
    plt.savefig(filename)
    plt.close()'''

def drawgraph(filename,title,plotdict,xlabel,ylabel,xlim=None,ylim=None,smooth=False,logscale=False):
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    #plt.xticks(np.arange(0,30,2))
    #plt.yticks(np.arange(0,450,20))
    #plt.ylim([-1,1000])
    legend_list = []

    plt.title(title)
    if ylim:
        plt.ylim(ylim)
    if xlim:
        plt.xlim(xlim)
    if logscale:
        plt.xscale('log')
    markers = ["s","o","*","d","D","x"]
    mrk_indx = 0
    for label,plotlist in plotdict.items():
        xy_list = zip(*plotlist)

        x=list(xy_list[0])
        y=list(xy_list[1])
        if not smooth:
            plt.plot(x,y,linewidth=2)
        else:
            x_smooth = np.linspace(min(x), max(x), 200)
            y_smooth = spline(x, y, x_smooth)
            plt.plot(x_smooth,y_smooth,linewidth=2)
        legend_list.append(label)
        mrk_indx += 1
    plt.legend(legend_list,loc = "lower right")
    #plt.show()
    plt.savefig(filename)
    plt.close()
        #legend_list.append(label)
    #plt.show()



    #