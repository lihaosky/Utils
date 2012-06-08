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

int cur_client_num = 0;
int client_num;
int port_num;
struct hostent *server;
struct sockaddr_in server_addr;
pthread_mutex_t client_num_mutex = PTHREAD_MUTEX_INITIALIZER;

void error_msg(const char *msg) {
	perror(msg);
	exit(0);
}

void* thread_fun() {
	int sockfd;
#ifdef kb 
	char write_buffer[KB];
	char recv_buffer[KB];
#endif

#ifdef mb
    char write_buffer[MB];
    char recv_buffer[MB];
#endif
	int ret;

#ifdef kb
	memset(write_buffer, 0, KB);
#endif

#ifdef mb
    memset(write_buffer, 0, MB);
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
	
	while (1) {
		ret = send(sockfd, write_buffer, sizeof(write_buffer), 0);
		if (ret < 0) {
			close(sockfd);
			return;
		}
	}
}



int main(int argc, char **argv) {
    if (argc != 4) {
        printf("Usage: a.out thread_num hostname portnumber\n");
        exit(0);
    }

	int i;
	int ret;
   	pthread_t threads[1000];
    struct timespec start, end;
    double elapse_time;


	client_num = atoi(argv[1]);
	port_num = atoi(argv[3]);
	
    printf("client_num is %d\n", client_num);
    printf("port_num is %d\n", port_num);

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
#ifdef kb
    printf("Throughput is %f kb/sec\n", 10 * client_num / elapse_time);
#endif

#ifdef mb
    printf("Throughput is %f kb/sec\n", 1024 * 10 * client_num / elaspe_time);
#endif
	
	exit(0);
}

	
	
	
