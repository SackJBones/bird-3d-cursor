# Bird 3D Cursor

The Bird is a mouse for spatial computing.

The Bird is a one-handed tool for controlling an arbitrary point in 3D space. It enables precise, smooth control over a point cursor beyond arm's reach in virtual environments, using hand tracking rather than controllers.

<p align="center">
  <img src="docs/Images/BirdSwooshes.gif" alt="Bird Movement Demo">
</p>

## What is the Bird?

The Bird is a novel interaction technique for spatial computing that solves a fundamental problem: how to smoothly control a precise point in 3D space with one hand. Unlike traditional interaction techniques:

- **Direct manipulation** is limited to arm's reach
- **Ray-casting** can point at distant objects but specifies a line, not a point, making it impossible to move objects closer or farther without additional mechanics

The Bird can be sent farther away or brought closer by extending and closing the fingers. As with a 2D computer mouse, the pointer finger is reserved for selecting.

It's called the Bird because it's a mouse that flies.

<p align="center">
  <img src="docs/Images/all-steps.gif" alt="Bird Movement Demo">
</p>

## Applications

The Bird opens up new possibilities for spatial computing:

- **Object Manipulation**: Select, move, and place objects anywhere in 3D space (including very far away, occluded locations)
- **3D UI**: Control UI elements anywhere in your field of view
- **Creative Tools**: Draw, sculpt, and manipulate in 3D space
- **Architecture & Design**: Place and arrange elements in large-scale environments
- **And more**: The possibilities are endless!

## Getting Started

### Prerequisites

- Unity 2020.3 or later
- A compatible VR/AR headset with hand tracking (Meta Quest, etc.)

### Installation

1. **Download the Unity Package**
   Download the latest `.unitypackage` file (`bird-3d-cursor/Unity/Bird3DCursor.unitypackage`)

2. **Import into Unity**
   In Unity, go to `Assets > Import Package > Custom Package` and select the downloaded file.

3. **Configure Hand Tracking**
   - Navigate to `Tools > Bird 3D Cursor > Configure Hand Tracking`
   - Select your preferred hand tracking API (OpenXR, Oculus OVR, or Leap)
   - Click "Apply Settings"

4. **Add to Your Scene**
   - Add a Bird Provider script to each tracked hand in your scene
   - Set the chirality (left/right) appropriately for each hand
   - Fill in the prefab and material slots with the provided assets

5. **Create Interactable Objects**
   - Add a Bird Interactable script to any object you want to interact with
   - Configure the interaction behavior (ActivateType, MotionType, etc.)
   - Assign the Bird Provider to the object

For detailed setup instructions, see the [Quick Start Guide](https://docs.google.com/document/d/1sneDWHFeR6_jMXwIwmY0Md-P3t-EXAoqKmOlfDvMss8/).

## How It Works

The Bird uses a sphere-of-best-fit algorithm to determine the position of the cursor. It uses the positions of the user's fingers to calculate a ray from the palm through the center of a sphere formed by the fingers. The cursor is positioned along this ray. The position of the cursor on the ray is determined by the size of the sphere.

The implementation includes:
- Jitter reduction using Kalman filtering
- Non-linear distance mapping for precision at different ranges

## Documentation

For more information, check out:
- [CHI 2025 Paper](https://doi.org/10.1145/3706599.3720045)
- [Quick Start Guide]((https://docs.google.com/document/d/1sneDWHFeR6_jMXwIwmY0Md-P3t-EXAoqKmOlfDvMss8/)
- [Original Thesis](https://dspace.mit.edu/handle/1721.1/142815)

## Beyond Unity

While this implementation is built for Unity, the core concepts of the Bird can be applied to any 3D environment. The Bird is not tied to any specific platform or framework:

- **Languages**: This C# implementation can be ported to other languages
- **Platforms**: Can be used in any spatial computing environment with hand tracking
- **Integration**: Suitable for AR/VR/XR, games, spatial computing OS integration, and general 3D UIs

We encourage adaptation and implementation of the Bird across different environments and use cases!

## Community & Support

- Join our [Developer Discord](https://discord.gg/JbZg4EDdKa) for implementation support and to connect with other developers

## Contributing

Contributions are welcome! Whether it's bug fixes, feature improvements, or ports to new platforms, we'd love to see what you build with the Bird.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Citation

If you use the Bird in your research, please cite:

```
Aubrey Simonson, Dana Gretton, and Casper Harteveld. 2025. Bird: A Point Cursor for Virtual Immersive Environments. In Extended
Abstracts of the CHI Conference on Human Factors in Computing Systems (CHI EA ’25), April 26-May 1, 2025, Yokohama, Japan. ACM,
New York, NY, USA, 9 pages. https://doi.org/10.1145/3706599.3720045
```

## Authors

- **Aubrey Simonson** - *Inventor* - Northeastern University
- **Dana Gretton** - *Math* - Massachusetts Institute of Technology
