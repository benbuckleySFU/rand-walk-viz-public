# rand-walk-viz-public

Interface for generating and visualizing random 3D lattice walks restricted to the positive octant.

# Installation

This is a project made in Unity version 2022.3. It should work across platforms (at least Windows and Mac OS). It can be run in the Unity player, or built as a standalone application. Consult learn.unity.com for tutorials on how to build and run Unity projects.

# File Outputs

Builds will out put to the Builds folder. Similarly, TextOutputs and StatsOutputs folders will have the text and statistics outputs for any given run of the program.

# Camera Controls:

- Drag MIDDLE mouse button to move 
- Drag RIGHT mouse button to rotate
- SCROLL to zoom in and out

# Stepset Options

The panels on the right have various options for visualizing and generating walks.

Click the Generate button in the bottom-right part of this panel to generate walks according to the current settings.

## Naive Generation

This option is the default, and it generates walks from a weighted stepset by sampling the steps one at a time, and rejecting a walk if it leaves the positive octant. The weights are decided by the user, with default weighting of 1 for each step.

## Boltzmann Generation

This option generates walks using a Boltzmann sampler.

Select a Boltzmann setting from the dropdown list, then select Submit Boltzmann Parameters to confirm your choice.

Since Boltzmann generation can return an object of any size, it is necessary to choose a Min and Max walk length in terms of the number of steps. Any walk shorter or longer, or which leaves the positive octant, is rejected.

# Timed Runs

This uses whatever settings have been selected in the Stepset Options panel, and generates the longest walks it can in the number of seconds given. This is mainly useful for Boltzmann generation.

# Visualization Options

A set of options for visualizing and animating walks.

By default, the colour of a step represents its position within a walk (i.e. the first step is red, the last step is magenta).

## Colour = XYZ Position

Instead of the default colour-coding, colour the steps so that their colour represents how close they are to each axis. Red = X, Green = Y, Blue = Z.

## Show Convex Hull

"Animate Walks" must be selected to use this option. This shows the convex hull of simultaneous positions in the set of walks being displayed.

## Import Walks

To import a set of walks, copy and paste a step series (or multiple step series) in the proper format.
