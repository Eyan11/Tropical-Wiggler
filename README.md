# Tropical Wiggler Recreation

This project is a recreation of the Tropical Wiggler capture from Mario Odyssey in Unity.

This video shows the project running with a comparison to Mario Odyssey: https://youtu.be/QFxZwI7uoZA

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE) file for details.

## Code Overview

This section provides an overview of each script and its responsibilities.

#### [InputManager](Tropical_Wiggler/Assets/Scripts/InputManager.cs):

Handles all input and emits some events, such as when the stretch input is pressed and released.

#### [SoundManager](Tropical_Wiggler/Assets/Scripts/SoundManager.cs):

Plays the body SFX by alternating audio sources so that only two body SFX's play at once. Each body has its own sound effect that plays when it is enabled or disabled. SoundManager also plays the "doink" SFX when a contraction finishes.

#### [PlayerController](Tropical_Wiggler/Assets/Scripts/PlayerController.cs):

Handles all movement/rotation when the player is NOT in stretch mode.

#### [LookAheadCamera](Tropical_Wiggler/Assets/Scripts/LookAheadCamera.cs):

Rotates the camera horizontally towards the velocity direction of the player. The speed of the camera movement is determined by two animation curves that reference the player's forward direction relative to the camera, as well as the height of the camera relative to the camera orbit height range. To mimic the camera in Mario Odyssey, I stop the camera look-ahead rotation when the camera is in a top-down position.

#### [StretchController](Tropical_Wiggler/Assets/Scripts/StretchController.cs):

Handles all of the head's movement/rotation. The head moves towards the player's input direction until reaching the max stretch distance. When the max stretch distance is reached, all movement input and velocity in the frontmost body's forward direction is removed so that the head can't move further away. The head's rotation is limited to a max rotation of 50 degrees, which is how far the head can rotate relative to the forward direction of the frontmost body.

The StretchController also manages the stretching state by subscribing to the InputManager's OnStretchInputChanged event. When the stretch input is released, the script checks if the player is grounded. If so, a forward contraction is triggered in the StretchBody script, otherwise a backwards contraction is triggered.

#### [StretchBody](Tropical_Wiggler/Assets/Scripts/StretchBody.cs):

Handles the movement/rotation of all 14 bodies as well as the tail. When a stretch starts, the tail and body are unparented so that the tail can be locked in place and the head can move independently of the bodies. As the head moves during stretching, a new body is enabled when the head or last body gets too far from the tail until all 14 bodies are spawned.

The bodies always move towards the body in front of them. Then, when all 14 bodies are spawned, the bodies are also anchored to the tail so that the tail, body, and head stay connected. When moving bodies towards the head, I start with the body closest to the head and loop through them until reaching the body closest to the tail, moving each one towards the body in front of it like a snake. When anchoring the bodies to the tail, it is the exact opposite. I loop through the bodies starting with the body closest to the tail and ending with the one closest to the head, then I check if the distance between the current body and the body behind it is too far. If so, I move it towards the body behind it. The body movement function also handles when bodies exceed the max rotation angle (60 degrees), which causes the bodies to quickly move towards the closest reset rotation angle (55 degrees) relative to the body in front of it. The body rotation function is simple since the max rotation is handled by the movement; it just rotates the bodies towards the body in front of them.

Body collision is handled in two functions. One is for discrete collision checks, which checks if the body is currently overlapping any colliders. If so, it moves it outside of the collider. The other is for continuous collision checks, which is a sphere cast from the old body position to the new one, and it handles it the same way as the discrete collision check. Every frame, each body moves towards the one in front of it, then a continuous and discrete collision check is done. Next, it is constrained to the tail, and finally, another continuous and discrete collision check occurs. This procedure is repeated multiple times for more accurate body movement and collision handling. If the number of iterations is lowered, body movement becomes less jittery, but other bugs can occur.

Finally, contractions are relatively simple. When a contraction starts, the current body positions are stored in an array, and the bodies use that array as a series of waypoints to follow until they reach the head (when contracting forward) or tail (when contracting backwards). When a body reaches the head/tail, it gets disabled and its SFX is played. A contraction finishes when all bodies are disabled and the head and tail reach each other. When this happens, the head/tail positions are updated and the tail and bodies get reparented to the same parent as the head.


## Disclaimer
This project is a fan-made demonstration and is not affiliated with, endorsed by, or associated with Nintendo. Mario and all related trademarks, characters, and intellectual property are owned by Nintendo. This project is intended for educational and non-commercial purposes only.
