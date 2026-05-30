#!/bin/bash
echo "Looking for any JWT references in Twitch API classes..."
grep -Rn -i "jwt" StreamActions.Twitch/Api/ || echo "No JWT references found"
