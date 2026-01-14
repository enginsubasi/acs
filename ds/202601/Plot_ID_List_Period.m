% MATLAB Script: CAN Analysis & High-Resolution Visual Fingerprint
% Logic: Fixed 20-byte transceiver protocol (Little-Endian ID)

clear; clc;close all;

%% 1. Data Loading
fileURL = 'CAN_20260114_1948-af-20-66-p4.csv'; 
opts = delimitedTextImportOptions("NumVariables", 2);
opts.VariableTypes = ["datetime", "string"];
opts.Delimiter = ",";

try
    dataTab = readtable(fileURL, opts);
catch ME
    error('File load error: %s', ME.message);
end

rawHex = dataTab{:, 2};
numRows = size(rawHex, 1);

%% 2. Extraction & ID Calculation
% Based on Protocol: B5=LSB, B8=MSB 
canIDValues = zeros(numRows, 1);
t = dataTab{:, 1};
timestamps = hour(t)*3600 + minute(t)*60 + second(t);

validCount = 0;
for i = 1:numRows
    bytes = sscanf(rawHex(i), '%x');
    
    % Header Check: 0xAA 0x55 
    if length(bytes) >= 9 && bytes(1) == 170 && bytes(2) == 85
        validCount = validCount + 1;
        
        % Little-Endian Extraction [cite: 1244, 1248]
        % MATLAB indices 6,7,8,9 correspond to Protocol Bytes 5,6,7,8
        canIDValues(validCount) = bytes(6) + ...
                                  bytes(7) * 256 + ...
                                  bytes(8) * 65536 + ...
                                  bytes(9) * 16777216;
    end
end

% Cleanup
canIDValues = canIDValues(1:validCount);
% We map timestamps only to the valid indices
all_timestamps = timestamps(1:validCount);

%% 3. Output Unique IDs and Average Periods
uniqueIDs = unique(canIDValues);

fprintf('\n==========================================\n');
fprintf('       CAN BUS STATISTICS (CORRECTED)     \n');
fprintf('==========================================\n');
fprintf('%-15s | %-15s\n', 'CAN ID (Hex)', 'Avg Period (s)');
fprintf('------------------------------------------\n');

avgPeriods = zeros(length(uniqueIDs), 1);

for j = 1:length(uniqueIDs)
    currID = uniqueIDs(j);
    idIdx = (canIDValues == currID);
    idTimes = all_timestamps(idIdx);
    
    if length(idTimes) > 1
        avgPeriods(j) = mean(diff(idTimes));
        fprintf('0x%08X      | %.4f\n', uint32(currID), avgPeriods(j));
    else
        avgPeriods(j) = 0;
        fprintf('0x%08X      | No diff (1 msg)\n', uint32(currID));
    end
end
fprintf('==========================================\n\n');

%% 4. Generate Maximum Square Image
imgSide = floor(sqrt(validCount));

if imgSide > 0
    displayData = canIDValues(1:imgSide^2);
    % Reshape so each row represents a sequence of IDs
    patternImg = reshape(displayData, [imgSide, imgSide])';

    figure('Name', 'CAN ID Attack Profile Fingerprint', 'Color', 'w');
    imagesc(patternImg);
    
    colormap(jet); 
    colorbar;
    axis image;
    axis off;
    
    title(sprintf('Visual Pattern: %d Unique IDs (%d x %d Packets)', ...
          length(uniqueIDs), imgSide, imgSide));
end