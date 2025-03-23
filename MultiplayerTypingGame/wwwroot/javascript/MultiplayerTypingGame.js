let socket = null;
// ON LOAD FUNCTION.    
onload = async () =>
{
    // RETRIEVE THE ROOMS.
    const getRoomsUrl = new URL("/getRooms", window.location.href);
    let response = await fetch(
        getRoomsUrl.href, 
        {
        headers: {'Accept': 'application/json'}
        });
    let roomsJson = await response.json();
    let rooms = roomsJson;
    
    // Add the rooms to the table.
    const roomTable = document.getElementById("RoomTable");
    const joinRoomTextbox =  document.getElementById("JoinRoomTextbox");
    const joinRoomButton = document.getElementById("JoinRoomButton");
    
    rooms.forEach((room) => {
        // Create the row with the room details.
        const roomRow = roomTable.insertRow(-1);
        roomRow.insertCell(-1).innerText = room.RoomKey;
        roomRow.insertCell(-1).innerText = room.Players[0].Name;
        roomRow.insertCell(-1).innerText = room.Players.length;

        // Join room when clicking on the row.
        roomRow.addEventListener('click', () => {
            joinRoomTextbox.value = roomRow.cells[0].innerText;
            joinRoomButton.click();
          });
    });

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
            document.getElementById("GameWindow").Close();
            const mainMenu = document.getElementById("MainMenu");
            mainMenu.style.display = "flex";
        });
    });

    // HANDLE THE USER JOINING A ROOM.
    joinRoomButton.addEventListener("click", (event) =>
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
            document.getElementById("GameWindow").Close();
            const mainMenu = document.getElementById("MainMenu");
            mainMenu.style.display = "flex";
        });
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
            case "promptAttack":
            {
                gameWindow.Attack(socket).then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "pendingPhrase":
            {
                gameWindow.DisplayPendingPhrase(message.phrase);
                break;  
            }
            case "promptDefense":
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
            case "opponentDisconnected":
            {
                gameWindow.Close();
                break;
            }
            case "ping":
            {
                const pongMessage = { type: "pong" };
                socket.send(JSON.stringify(pongMessage));
                break;
            }
            default:
            {
                gameEndPromiseResolver("Game ended due to unknown reason.");
                break;
            }
        }
    }

    // CREATE THE PROMISE TO BE RESOLVED WHEN THE GAME ENDS.
    return (new Promise((resolve) =>
    {
        gameEndPromiseResolver = resolve;
    })).then(() =>
    {
        gameWindow.Close();
    });
}
