// UnityAssets.Test.cpp : This file contains the 'main' function. Program execution begins and ends there.
//


#include <iostream>
#include <windows.h>

#include <comutil.h>
#include <iostream>
#include <string>
#include <vector>
#include <istream>
#include <fstream>
#pragma comment(lib,"comsuppw.lib")

typedef int(WINAPI* GetPngFromHeroPfn)(const wchar_t*, const byte*);

#define BUFFER_SIZE (1024 * 1024 * 5)

int main()
{
	std::cout << "Loading dll..." << "\n";
	HMODULE unityAssetsDll{ LoadLibrary(L"UnityAssets.dll") };
	if (!unityAssetsDll) return 1;

	std::cout << "Call GetPngFromHero" << "\n";
	GetPngFromHeroPfn GetPngFromHero{ (GetPngFromHeroPfn)GetProcAddress(unityAssetsDll, "GetPngFromHero") };
	byte* buf{ (byte*)malloc(BUFFER_SIZE) };
	long outSize{ GetPngFromHero(L"2470", buf) };
	std::cout << "Output size: " << outSize;
	return 0;
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
