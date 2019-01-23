import os
import clr

clr.AddReference("System")
clr.AddReferenceToFile("lib/SynerdocsExtensions.dll")

from SynerdocsExtensions import *
from System import Array


def update_contract_number():
  defaultContractNumber = "Д-19/5"
  contractNumber = "Д-19/1"
  update_samples_params(defaultContractNumber, contractNumber)


def update_samples_params(oldValue, newValue):
  samplesDir = "samples"
  for root, subFolders, files in os.walk(samplesDir):
    for folder in subFolders:
      for folder, subFolders, files in os.walk(os.path.join(root, folder)):
        for file in files:
          extension = os.path.splitext(file)[1]
          if extension == ".xml":
            filePath = str(os.path.join(folder, file))
            try:
              replace_string_in_file(filePath, oldValue, newValue)
            except Exception as e:
              print("Document changing error: " + str(e))
            else:
              print("Document changed succesfully (" + filePath + ")")

  print("All documents contracts numbers changed")


def replace_string_in_file(path, oldValue, newValue):
  with open(path, 'r') as file:
    filedata = file.read().decode('cp1251')
  filedata = filedata.replace(oldValue, newValue)
  with open(path, 'w') as file:
    file.write(filedata.encode('cp1251'))


def send_documents():
  tin = "1835128323"
  thumbprint = "‎30b8258e8f217650bb82c0fd33d259d5214f35ce"
  samplesDir = "samples"
  for root, subFolders, files in os.walk(samplesDir):
    for folder in subFolders:
      paths = []
      for folder, subFolders, files in os.walk(os.path.join(root, folder)):
        for file in files:
          paths.append(str(os.path.join(folder, file)))
      try:
        Messages.SendDocuments(thumbprint, tin, Array[str](paths))
      except Exception as e:
        print("Message not sent. Error: " + str(e))
      else:
        print("Message sent succesfully (" + folder + ")")
  print("All documents sent")


update_contract_number()
send_documents()
