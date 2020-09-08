Freelancer Open Server by FriendlyFire and Cannon

Why? Why? Why?

As we pass the 10 year aniversary, we've attempted the supposed impossible and seem to have somewhat succeeded. This project is built upon the shoulders the open source projects and developers in the Freelancer community. Without the release of code by different people, this project would have been impossible. Some notable projects that directly contributed to this:
- UTFEditor (both the new C# one and the original)
- PacketDump (thanks Adoxa!)
- FLHook (thanks Horst! Motah, Wodka, Crazy and you know who you all are!)
- DSAM/DSPM
- [that quaternion tool on tsp]
- SurDump (thanks again Adoxa!) 

Build Instructions

- Does it stop with "locker lock exception" or something? If it's this, just hit continue in the debugger. It won't happen in the release build.
- Have you edited the flopenserver.cfg file to have the right paths, etc? This file is copied to the bin directory on build - so rebuild the sw to check that the latest is copied over.
- Your game accounts directory - it needs a copy of "default.fl" from the "Accts" directory in the src directory - or make that "Accts" directory your game accounts directory. This file is the template used to make new characters.