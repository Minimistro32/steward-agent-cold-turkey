# Command Line Parameters

Evidence: embedded help in the supplied Mach-O executable; not executed on the target Mac. See [feasibility findings](README.md) for provenance and limitations. The initial verb requires a leading dash in the embedded syntax. Use `-help` to compare the installed version.

To run Blocker from the command line on Windows, please close the interface by pressing X and then right click the system tray icon and select exit.

On Windows, Blocker can be run from the command prompt with the following command, including the quotes:

>"C:\Program Files\Cold Turkey\Cold Turkey Blocker.exe" [options]

On macOS, Blocker can be run from Terminal with the command, including the quotes:

>"/Applications/Cold Turkey Blocker.app/Contents/MacOS/Cold Turkey Blocker" [options]

Where [options] must be one of following formats, excluding the square brackets:

  - `-start "Block Name"`<br>
  Starts the specified block with no lock. Website & App blocks will be
  blocked at all times. Device blocks will be enabled if scheduled. To start a
  device block immediately, use the -lock parameter as well.

  - `-start "Block Name" -as-is`<br>
  Starts the specified block with all previously configured settings, including
  block type and lock.

  - `-start "Block Name" -lock X`<br>
  Starts and locks the specified block for X number of minutes
  (X must be >= minutes left). If the name of a device block is given, X is
  the duration of the block in minutes.

  - `-start "Block Name" -password X`<br>
  Starts and locks the specified block with X as the password. Spaces and
  quotes can't be used when setting a password via the command line. Only
  scheduled device blocks can be locked using a password.

  - `-start "Block Name" -random-text X`<br>
  Starts and locks the specified block with X number of random characters
  (words). Only scheduled device blocks can be locked using random text.

  - `-stop "Block Name"`<br>
  Stops the specified block if unlocked. Device blocks will be disabled if
  scheduled.

  - `-stop "Block Name" -password X`<br>
  Stops the specified block if locked with X as the block password.

  - `-toggle "Block Name"`<br>
  Starts or stops the specified block as if -start and -stop were used.

  - `-status "Block Name"`<br>
  Displays if the specified block is enabled or disabled.

  - `-list-blocks`<br>
  Displays a list of all blocks.

  - `-add-block "Block Name"`<br>
  Creates a new website & app block with the specified name.

  - `-add "Block Name" -web "URL"`<br>
  Adds a URL to the website tab of the specified (non-device) block. The block
  must already exist.

  - `-add "Block Name" -exception "URL"`<br>
  Adds a URL to the exception tab of the specified (non-device) block if
  unlocked. The block must already exist.

  - `-add-device-block "Block Name"`<br>
  Creates a new device block (lock screen) with the specified name.

  - `-add-device-block "Block Name" -sign-out`<br>
  Creates a new device block (sign out) with the specified name.

  - `-add-device-block "Block Name" -shut-down`<br>
  Creates a new device block (shut down) with the specified name.

  - `-start-delay-break "Block Name"`<br>
  Starts the countdown for delay break (if set) for the specified block.

  - `-stop-delay-break "Block Name"`<br>
  Stops a delay break (if started) for the specified block.

  - `-stop-random-text-break "Block Name"`<br>
  Stops a random text break (if started) for the specified block.

  Additional constraints embedded in the binary:
- Block names are case-sensitive.
- Password locks require Blocker Pro. Command-line passwords cannot contain spaces, backslashes, or quotation marks.
- Random-text length must be 1–5000.
- A lock duration cannot shorten an existing lock.
- Password protection on editing or block status can prevent commands from succeeding.
