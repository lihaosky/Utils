if [ -f 'time.txt' ]
then
rm 'time.txt'
fi

if [ $# -ne 1 ]
then
echo 'Usage: ./load_recorder.sh [hour]'
exit
fi

min=$1

i=0
while [ $i -lt $min ]
do
ct=`date +%s`
a=`ps -eo pcpu | sort -k 1 -r | head -10`
load=`echo $a | awk -F" " '{print $2+$3+$4+$5+$6+$7+$8+$9+$10}'`
echo -n $ct >> time.txt
echo -n ' ' >> time.txt
echo $load >> time.txt
i=$(($i+1))
sleep 60
done
