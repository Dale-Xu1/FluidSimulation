# Fluid Simulation

## Overview

This is a real-time Eulerian (grid-based) fluid simulation written as an HLSL compute shader running on the GPU with DirectX 11. The simulation computes an approximation of a solution to the Navier-Stokes Equations, and also simulates turbulence through curl noise.

## Instructions

.NET via the dotnet CLI is required to run the program. The repository includes the launch files for VSCode, which can be used to start a development build of the program.

## Possible Improvements

Currently, boundary conditions are not being checked, and the simulation acts as if fluid may exit the bounds of the screen.
