starttime=$1
finishtime=$2
#echo 'Starting time is '$starttime
#echo 'Finishing time is '$finishtime
totalload=0
totaltime=0
while read line
do
#echo $line
time=`echo $line | awk -F" " '{print $1}'`
load=`echo $line | awk -F" " '{print $2}'`
if [ "$time" != "" ] && [ $time -gt $starttime ] && [ $time -lt $finishtime ]
then
totalload=`echo $totalload+$load | bc`
totaltime=$((totaltime+1))
#echo $totalload
#echo $totaltime
fi
done < 'time.txt'
averageload=`echo "scale=2;$totalload/$totaltime" | bc`
echo "Average load is $averageload"
echo $averageload >> load.txt
