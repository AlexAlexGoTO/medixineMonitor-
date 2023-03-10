import React, { useState } from 'react';
import { useEffect } from 'react';
import EditObservation from './EditObservation';
import Observation from './Observation';
import './observations.css';
import * as signalR from "@microsoft/signalr";

const API_URL = 'https://localhost:7289'

const Observations = () => {
    const [selectedRow, setSelectedRow] = React.useState("-1");
    const [observations, setObservations] = useState([]);
    const [patients, setPatients] = useState([])

    const getObservations = async () => {
        const response = await fetch(`${API_URL}/observations`);
        const data = await response.json();

        setObservations(data);
    }
    
    const getPatients = async () => {
        const response = await fetch(`${API_URL}/patients`);
        const data = await response.json();

        setPatients(data);
    }

    useEffect(() => {
        getObservations();

        getPatients();

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_URL}/monitor-updates`, {
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect()
            .build();

        connection.start();

        connection.on("observations-update", (data) => {
            setObservations(prevState => 
                {
                    const index = prevState.findIndex(o => o.id === data.id);
                    if(index > -1) {
                        return prevState.map(item => 
                                    item.id === data.id 
                                    ? {...item, 
                                        patientId : data.patientId,
                                        name : data.name, 
                                        type: data.type, 
                                        value: data.value,
                                        description: data.description
                                    } 
                                    : item )
                    } else {
                        return [...prevState, data];
                    }
                }
            )
        });
    }, [])

    return (
        <div className="row">
            <div className="itemsList col-md-6">
                <table className="table">
                    <thead className="thead-dark">
                        <tr>
                        <th scope="col">PatientId</th>
                        <th scope="col">Name</th>
                        <th scope="col">Type</th>
                        <th scope="col">Value</th>
                        <th scope="col">Description</th>
                        </tr>
                    </thead>
                    <tbody>
                        {observations.map((observation) => (
                            <tr className="data" key={observation.id} onClick={() => setSelectedRow(observation.id)}>
                                <Observation observation={observation}/>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <div className="editObservation col-md-6">
                <button onClick={() => setSelectedRow("")} className="btn btn-primary create-item">
                    Create New
                </button>
                <button onClick={() => setSelectedRow(-1)} className="btn btn-dark cancel-item">
                    Cancel
                </button>
                {selectedRow != -1 && <EditObservation oData={{selectedRow, patients}}/> }
            </div>
        </div>
    )
}

export default Observations;