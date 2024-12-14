let socket = null;
// ON LOAD FUNCTION.    
onload = async () =>
{
    // HANDLE THE USER CREATING A ROOM.
    const createRoomButton =  document.getElementById("CreateRoomButton");
    createRoomButton.addEventListener("click", () =>
    {
        const createRoomUrl = new URL("/createRoom", window.location.href);
        socket = new WebSocket(createRoomUrl.href);
        socket.onclose = () =>
        {
            console.log("websocket closed.");
        };
        startGame(socket).then((reason) =>
        {
            console.log(reason)
            // GO BACK TO THE MAIN MENU.
            document.getElementById("GameWindow").Close;
            const mainMenu = document.getElementById("MainMenu");
            mainMenu.style.display = "flex";
        });
    });

    // HANDLE THE USER JOINING A ROOM.
    const joinRoomTextbox =  document.getElementById("JoinRoomTextbox");
    joinRoomTextbox.addEventListener("keypress", (event) =>
    {
        if (event.key == "Enter")
        {
            const joinRoomUrl = new URL("/joinRoom", window.location.href);
            joinRoomUrl.searchParams.append("roomKey", joinRoomTextbox.value.trim());
            socket = new WebSocket(joinRoomUrl.href);
            socket.onclose = () =>
            {
                console.log("websocket closed.");
            };
            startGame(socket).then((reason) =>
            {
                console.log(reason)
                // GO BACK TO THE MAIN MENU.
                document.getElementById("GameWindow").Close;
                const mainMenu = document.getElementById("MainMenu");
                mainMenu.style.display = "flex";
            });
        } 
    });
}

startGame = async (socket) =>
{
    // HIDE THE MAIN MENU.
    const mainMenu = document.getElementById("MainMenu");
    mainMenu.style.display = "none";
    
    // HANDLE THE SOCKET DISCONNECTING.
    let gameEndPromiseResolver;
    socket.onclose = () => { gameEndPromiseResolver("The connection was closed by the server."); };
    socket.onerror = () => { gameEndPromiseResolver("The connection was closed due to a connection error."); };
    
    // HANDLE MESSAGES FROM THE WEBSOCKET CONNECTION.
    const gameWindow = document.getElementById("GameWindow");
    socket.onmessage = (event) =>
    {
        const message = JSON.parse(event.data);
        switch (message.type)
        {
            case "created":
            {
                // Show the room key.
                const roomKeySpan = document.getElementById("RoomKeySpan");
                roomKeySpan.style.display = "block";
                const roomKeyValueSpan = document.getElementById("RoomKeyValueSpan");
                roomKeyValueSpan.innerText = message.roomKey;
                break;
            }
            case "start":
            {
                // Hide the room key.
                const roomKeySpan = document.getElementById("RoomKeySpan");
                roomKeySpan.style.display = "none";

                // SHOW THE GAME.
                gameWindow.Open()

                break;
            }
            case "attack":
            {
                gameWindow.Attack().then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "defend":
            {
                gameWindow.Defend(message.phrase).then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "result":
            {
                gameWindow.ShowResult(message);                
                break;
            }
            default:
                gameEndPromiseResolver("Game ended due to unknown reason.");
                break;
        }
    }

    // CREATE THE PROMISE TO BE RESOLVED WHEN THE GAME ENDS.
    return new Promise((resolve) =>
    {
        gameEndPromiseResolver = resolve;
    });
}
