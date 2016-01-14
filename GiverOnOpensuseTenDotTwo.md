# Introduction #

Aaron Bockover wrote a summary of how to get Giver running on Opensuse 10.2

# Details #

How to run Giver on openSUSE 10.2, assuming you have a full GNOME and
Mono devel stack installed for build and runtime deps:

  * sudo smart remove avahi libdaemon
  * Download libdaemon 0.12
  * Download avahi 0.6.20
  * Build libdaemon: ./configure --prefix=/usr && make && sudo make install
  * Build Avahi: ./configure --prefix=/usr --sysconfigdir=/etc --disable-qt3 --disable-qt4 && make && sudo make install
  * Build giver from source




