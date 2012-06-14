#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <string.h>
#include <sys/types.h>
#include <netinet/in.h>
#include <netdb.h>
#include <pthread.h>
#include <time.h>

#define N (1000000000)
#define MB 1024*1024
#define KB 1024
#define BSIZE 64*1024

int cur_client_num = 0;
int client_num;
int port_num;
int total_sent = 0;
int total_to_send = 0;
struct hostent *server;
struct sockaddr_in server_addr;
pthread_mutex_t client_num_mutex = PTHREAD_MUTEX_INITIALIZER;

void error_msg(const char *msg) {
    perror(msg);
    exit(0);
}

void* thread_fun() {
    int sockfd;
    int counter = 10;
    int sent = 0;
#if defined kb 
    char write_buffer[KB];
    char recv_buffer[KB];
#elif defined  mb
    char write_buffer[MB];
    char recv_buffer[MB];
#else 
    char write_buffer[BSIZE];
    char recv_buffer[BSIZE];
#endif
    int ret;

#if defined kb
    memset(write_buffer, 0, KB);
#elif defined mb
    memset(write_buffer, 0, MB);
#else
    memset(write_buffer, 0, BSIZE);
#endif
	
    sockfd = socket(AF_INET, SOCK_STREAM, 0);
    if (sockfd < 0) {
	error_msg("Error opening socket");
    }

    if (connect(sockfd, (struct sockaddr*)&server_addr, sizeof(server_addr)) < 0) {
	error_msg("Error connecting to server");
    }

    pthread_mutex_lock(&client_num_mutex);
    cur_client_num++;
    pthread_mutex_unlock(&client_num_mutex);

    while (cur_client_num != client_num);

    printf("Starting sending!");
	
    while (sent < total_to_send) {
        if (sizeof(write_buffer) < (total_to_send - sent)) {
	    ret = send(sockfd, write_buffer, sizeof(write_buffer), 0);
        } else {
            ret = send(sockfd, write_buffer, total_to_send - sent, 0);
        }

    	if (ret < 0) {
            printf("Remote side closed socket!");
            close(sockfd);
	    return;
    	}

        pthread_mutex_lock(&client_num_mutex);
        total_sent += ret;
        pthread_mutex_unlock(&client_num_mutex);
        sent += ret;

        printf("Sents bytes are %d\n", sent);
    }
}



int main(int argc, char **argv) {
    if (argc != 5) {
        printf("Usage: a.out thread_num hostname portnumber total_to_send(bytes)\n");
        exit(0);
    }

    int i;
    int ret;
    pthread_t threads[1000]; /* Maximum 1000 threads... */
    struct timespec start, end;
    double elapse_time;


    client_num = atoi(argv[1]);
    port_num = atoi(argv[3]);
    total_to_send = atoi(argv[4]);

    printf("Client_num is %d\n", client_num);
    printf("Host name is %s\n", argv[2]);
    printf("Port_num is %d\n", port_num);
    printf("Total bytes to send is %d\n", total_to_send);
#if defined kb
    printf("Send buffer size is %d\n", KB);
#elif defined mb
    printf("Send buffer size is %d\n", MB);
#else 
    printf("Send buffer size is %d\n", BSIZE);
#endif

    if (client_num > 500) {
	fprintf(stderr, "Too many threads!\n");
	exit(0);
    }
	
    server = gethostbyname(argv[2]);
    if (server == NULL) {
	fprintf(stderr, "ERROR, no such host\n");
	exit(0);
    }
	
    bzero((char*)&server_addr, sizeof(server_addr));
    server_addr.sin_family = AF_INET;
    bcopy((char*)server->h_addr, (char*)&server_addr.sin_addr.s_addr, server->h_length);
    server_addr.sin_port = htons(port_num);
	
    clock_gettime(CLOCK_MONOTONIC, &start);
    for (i = 0; i < client_num; i++) {
	ret = pthread_create(&threads[i], NULL, thread_fun, NULL);
	if (ret) {
		fprintf(stderr, "Failed to create thread %d\n", i);
		exit(0);
	}
    }

    for (i = 0; i < client_num; i++) {
	pthread_join(threads[i], NULL);
    }

    clock_gettime(CLOCK_MONOTONIC, &end);
    elapse_time = (double)((int64_t)end.tv_sec * N + (int64_t)end.tv_nsec - (int64_t)start.tv_sec * N - (int64_t)start.tv_nsec) / N;

    printf("Elapsed time is %f seconds\n", elapse_time);
    printf("Throughput is %f kb/sec\n", total_sent / elapse_time / 1024);

    exit(0);
}
