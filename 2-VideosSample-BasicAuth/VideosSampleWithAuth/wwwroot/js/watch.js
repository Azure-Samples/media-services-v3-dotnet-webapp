//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

function playVideo(data, bearer) {
    const title = document.getElementById('title');
    title.innerHTML = data.title;

    var options = {
        autoplay: true,
        controls: true,
        width: "1000",
        height: "600",
        logo: { enabled: false },
    };
    var player = amp('player', options);
    player.src(
        [
            {
                src: data.locator,
                type: "application/vnd.ms-sstr+xml",
                protectionInfo: [
                    {
                        type: "AES",
                        authenticationToken: bearer
                    },
                    {
                        type: "PlayReady",
                        authenticationToken: bearer
                    },
                    {
                        type: "Widevine",
                        authenticationToken: bearer
                    }
                ]
            },
        ]);
}

function loadVideo() {
    getTokenPopup(tokenRequest)
        .then(response => {
            if (response) {
                console.log("access_token acquired at: " + new Date().toString());
                try {
                    const headers = new Headers();
                    const bearer = `Bearer ${response.accessToken}`;

                    headers.append("Authorization", bearer);

                    const options = {
                        method: "GET",
                        headers: headers
                    };

                    fetch('/videos/' + videoId, options)
                        .then(response => response.json())
                        .then(data => playVideo(data, bearer));
                } catch (error) {
                    console.warn(error);
                }
            }
        });
}

const params = new URLSearchParams(window.location.search);
const videoId = params.get('video');

loadVideo();