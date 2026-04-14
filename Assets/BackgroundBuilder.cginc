//the function int depthOfTexel(int2) MUST be defined BEFORE this is included in order for this to work!
//must also #define dirCount BEFORE this is included; otherwise it will be 2
//optionally also #define EXPONENTIALTESTS if tests should go out exponentially instead of linearly
#include "DirectionDefinitions.cginc"

inline uint4 GenerateBackground(int2 startPos, int testNum, float minObjectDepth, float projectionMod, float maxDepDiff, int defaultThickness) {
	int origDep = depthOfTexel(startPos);

	if (origDep >= 30) {
		return uint4(0, defaultThickness, 0, 0); //fully thick layer1; otherwise irrelevant data so just 0
	}

    dirDef //see DirectionDefinitions.cginc

	int lDist[dirCount], rDist[dirCount], lDep[dirCount], rDep[dirCount];
	[unroll(dirCount)]
	for (uint b = 0; b < dirCount; b++) { lDist[b] = 0; rDist[b] = 0; lDep[b] = 0; rDep[b] = 0; }
	uint bestDir = 8;

#ifdef EXPONENTIALTESTS
    int c = 1; //define c elsewhere
	[loop]
	for (int realCounter = 0; realCounter < testNum; realCounter++, c=c<<1) { //double c each time
#else
	[loop] //unrolling it 22 times is actually significantly slower (just over half speed)
	for (int c = 1; c <= testNum; c++) {
#endif
		int halfC = (int)(minObjectDepth + c * 0.5f * projectionMod);
		int targetDep = origDep + halfC;
		if (targetDep >= 29) { //no longer possible to find any viable pixels, so just break out of the loop
			break;
		}

		[unroll(dirCount)] //for some reason it seems FASTER in the editor when I use [loop], but surely unroll ought to be better
		//for (uint d = 0; d < dirCount; d++) {
        for (uint d = dirCount-1; d >= 0; d--) { //go backwards if we're not including breaks, because I prefer the earlier directions
#if dirCount > 4
			int2 offset = dir[d] * c;
#else
			int2 offset = (dir[d] * c) / 2;
#endif
			if (lDist[d] == 0) {
				int dep = depthOfTexel(startPos + offset);
				if (dep > targetDep) {
					lDist[d] = c;
					lDep[d] = dep;
					if (rDist[d] > 0) {
						bestDir = d;
						//break; //it seems to be slightly faster without breaks, especially when using [unroll]
					}
				}
			}
			if (rDist[d] == 0) {
				int dep = depthOfTexel(startPos - offset);
				if (dep > targetDep) {
					rDist[d] = c;
					rDep[d] = dep;
					if (lDist[d] > 0) {
						bestDir = d;
						//break;
					}
				}
			}
		}

		if (bestDir < 8) { //we've already made our pick
			break;
		}
	}

	if (bestDir < 8) { //a ray had both sides hit

		int dist2 = rDist[bestDir];
		int layer1thick = clamp(ceil(minObjectDepth + (lDist[bestDir] + rDist[bestDir]) * 0.5f * projectionMod), 1, 31);
		int l2Dep = 0;
		int dist1 = lDist[bestDir]; //must be int so bit operation works

		bool lSky = lDep[bestDir] >= 30; //don't interpolate between sky and not-sky
		bool rSky = rDep[bestDir] >= 30;
		if (lSky == rSky && abs(lDep[bestDir] - rDep[bestDir]) <= layer1thick * maxDepDiff) {
			float totalDist = lDist[bestDir] + rDist[bestDir];
			l2Dep = round((lDep[bestDir] * rDist[bestDir] + rDep[bestDir] * lDist[bestDir]) / totalDist); //basically a weighted average, where the weight of lDep = rDist
		}
		else { //the two pixels are too different to interpolate between, so just pick the closest one
			if (rDist[bestDir] < lDist[bestDir]) { //right is closer
				dist1 = 0;
				l2Dep = rDep[bestDir];
			}
			else { //left is closer
				dist2 = 0;
				l2Dep = lDep[bestDir];
			}
		}

		return uint4(
			dist2,
			layer1thick,
			l2Dep,
			dist1 | (bestDir << 5)
		);
	}

		//no rays hit both sides; let's see if any rays had at least 1 side hit, though

		//find the shortest distance first
	int minDist = testNum+1;
	[unroll(dirCount)]
	for (uint k = 0; k < dirCount; k++) {
		if (lDist[k] > 0 && lDist[k] < minDist) {
			minDist = lDist[k];
		}
		if (rDist[k] > 0 && rDist[k] < minDist) {
			minDist = rDist[k];
		}
	}

	if (minDist > testNum) { //absolutely no background for this
		return uint4(0, defaultThickness, 0, 0);
	}

		//find the greatest depth that matches the shortest distance
	int maxDep = 0;
	[unroll(dirCount)]
	for (uint l = 0; l < dirCount; l++) {
		if (lDist[l] == minDist && lDep[l] > maxDep) {
			maxDep = lDep[l];
			bestDir = l;
		}
		if (rDist[l] == minDist && rDep[l] > maxDep) {
			maxDep = rDep[l];
			bestDir = l;
		}
	}

		//pack info into bytes
	int layer1thick = clamp(ceil(minObjectDepth + (minDist + testNum+1) * 0.5f * projectionMod), 1, 31);
	return uint4(
		rDist[bestDir],
		layer1thick,
		maxDep,
		lDist[bestDir] | (bestDir << 5)
	);
}