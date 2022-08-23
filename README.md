## Fluid-Simulator

<img src="documentation/images/fluid_sim.gif"/>

Volumetric GPU-Based fluid simulator implemented for unity. It is simulated with the Navier-Stokes Equations with the simplified assumption of incompressible, homogeneous fluid.

This project is meant to be used as a template to study fluid dynamics.

# Usage
The fluid simulator is composed of four components. The main one is the fluid field, and the reset act on the fluid field as follows:
* Fluid Injector: inject or suck the density from the fluid container
* Fluid Motors: add velocity to the container
* Fluid Collider: similar to rigid body collider

# References
* Jos Stam. Real-Time Fluid Dynamics for Games. Proceedings of the Game Developer Conference. 2003.
* Mark Harris. Fast Fluid Dynamics Simulation on the GPU. In GPU Gems: Programming Techniques, Tips, and Tricks for Real-Time Graphics (Chapter 38). 2004.
* Real-Time Simulation and Rendering of 3D Fluids. In GPU Gems 3 : Programming Techniques, Tips, and Tricks for Real-Time Graphics (Chapter 30). 2007.