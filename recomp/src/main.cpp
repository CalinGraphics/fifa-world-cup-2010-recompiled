// fifawc10 - ReXGlue Recompiled Project

#include "generated/default/fifawc10_init.h"

#include "fifawc10_app.h"

#ifdef _WIN32
// Laptops with two GPUs: the NVIDIA and AMD drivers look for these in the exe
// (not in a DLL) and then run the game on the dedicated GPU.
extern "C" {
__declspec(dllexport) unsigned long NvOptimusEnablement = 1;
__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
}
#endif

REX_DEFINE_APP(fifawc10, Fifawc10App::Create)
