#define fullDirDef int2 dir[8];dir[0] = int2(2, 0);dir[1] = int2(0, 2);dir[2] = int2(2, 2);dir[3] = int2(2, -2);dir[4] = int2(2, 1);dir[5] = int2(1, 2);dir[6] = int2(2, -1);dir[7] = int2(1, -2);
#if dirCount > 4
    #define dirCount 8
    #define dirDef fullDirDef
#elif dirCount > 2
    #define dirCount 4
    #define dirDef int2 dir[4];dir[0] = int2(1, 0);dir[1] = int2(0, 1);dir[2] = int2(1, 1);dir[3] = int2(1, -1);
#else
    #define dirCount 2
    #define dirDef int2 dir[2];dir[0] = int2(1, 0);dir[1] = int2(0, 1);
#endif